using System.Diagnostics;
using System.Text;
using AStarDev.CloudSync.Errors;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.OneDrive;

/// <summary>Keeps the token cache in the freedesktop Secret Service through the <c>secret-tool</c> CLI. There is no file fallback: when the keyring is unavailable, the call fails.</summary>
internal sealed class SecretToolTokenCacheStore(string executable = "secret-tool") : ITokenCacheStore
{
    private const string Unavailable = "The Secret Service keyring is unavailable, so the OneDrive sign-in cannot be stored. Start a keyring (for example gnome-keyring or KWallet) and install libsecret's secret-tool. Tokens are never written to a file.";

    private const int ChunkSize = 4096;

    private static readonly string[] Attributes = ["service", "astardev-cloudsync", "account", "onedrive"];

    /// <inheritdoc/>
    /// <remarks>The cache is read from chunk entries 0, 1, 2 and so on until one is missing. No chunk 0, including an old unchunked entry, means nothing stored, so it only costs one fresh sign-in. Text that is not valid base64 is treated as nothing stored too.</remarks>
    public async Task<Result<byte[], ProviderError>> LoadAsync(CancellationToken cancellationToken)
    {
        var text = new StringBuilder();
        for (var index = 0; ; index++)
        {
            var run = await RunAsync(["lookup", .. ChunkAttributes(index)], null, cancellationToken).ConfigureAwait(false);

            switch (run.ToResult(AuthenticationErrors.From))
            {
                case Ok<ToolRun> { Value.ExitCode: 0 } ok:
                    text.Append(ok.Value.StdOut.Trim());
                    continue;
                case Ok<ToolRun> { Value: { ExitCode: 1, StdErr: "" } }:
                    return Decode(text.ToString());
                default:
                    return new Fail<ProviderError>(new AuthenticationFailed(Unavailable));
            }
        }
    }

    /// <inheritdoc/>
    /// <remarks>Every earlier entry is cleared first, then the base64 text is stored in chunks small enough for one Secret Service entry. A failure part way leaves an incomplete cache that the next load rejects as corrupt.</remarks>
    public async Task<Result<Unit, ProviderError>> SaveAsync(byte[] cache, CancellationToken cancellationToken)
    {
        var cleared = await ClearAsync(cancellationToken).ConfigureAwait(false);
        if (cleared.Value is Fail<ProviderError>) return cleared;

        var text = Convert.ToBase64String(cache);
        for (var index = 0; index * ChunkSize < text.Length; index++)
        {
            var chunk = text.Substring(index * ChunkSize, Math.Min(ChunkSize, text.Length - index * ChunkSize));
            var run = await RunAsync(["store", "--label=AStarDev CloudSync OneDrive", .. ChunkAttributes(index)], chunk, cancellationToken).ConfigureAwait(false);

            if (run.ToResult(AuthenticationErrors.From).Value is not Ok<ToolRun> { Value.ExitCode: 0 }) return new Fail<ProviderError>(new AuthenticationFailed(Unavailable));
        }

        return new Ok<Unit>(default);
    }

    /// <inheritdoc/>
    /// <remarks>Exit code 1 with nothing on stderr means there was no entry, which counts as cleared.</remarks>
    public async Task<Result<Unit, ProviderError>> ClearAsync(CancellationToken cancellationToken)
    {
        var run = await RunAsync(["clear", .. Attributes], null, cancellationToken).ConfigureAwait(false);

        return run.ToResult(AuthenticationErrors.From) switch
        {
            Ok<ToolRun> { Value.ExitCode: 0 } => new Ok<Unit>(default),
            Ok<ToolRun> { Value: { ExitCode: 1, StdErr: "" } } => new Ok<Unit>(default),
            _ => new Fail<ProviderError>(new AuthenticationFailed(Unavailable))
        };
    }

    private static string[] ChunkAttributes(int index) => [.. Attributes, "chunk", index.ToString(System.Globalization.CultureInfo.InvariantCulture)];

    private static Result<byte[], ProviderError> Decode(string text)
        => Try.Run(() => Convert.FromBase64String(text.Trim())).ToResult(AuthenticationErrors.From) switch
        {
            Ok<byte[]> ok => ok,
            Fail<ProviderError> => new Ok<byte[]>([])
        };

    /// <summary>Returns the started process, or throws <see cref="InvalidOperationException"/> carrying a help link when the operating system started nothing.</summary>
    /// <param name="started">The result of <see cref="Process.Start(ProcessStartInfo)"/>, which is <c>null</c> when no process was started.</param>
    /// <param name="executable">The tool name used in the exception message.</param>
    internal static Process Started(Process? started, string executable)
        => started ?? throw new InvalidOperationException($"{executable} did not start.").WithHelpLink(OneDriveErrorCodes.SecretToolDidNotStart);

    private Task<Exceptional<ToolRun>> RunAsync(string[] arguments, string? stdin, CancellationToken cancellationToken)
        => Try.RunAsync(async token =>
        {
            var info = new ProcessStartInfo(executable)
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var argument in arguments)
            {
                info.ArgumentList.Add(argument);
            }

            using var process = Started(Process.Start(info), executable);
            using var killOnCancel = token.Register(() => process.Kill());
            await process.StandardInput.WriteAsync(stdin ?? string.Empty).ConfigureAwait(false);
            process.StandardInput.Close();
            var stdout = process.StandardOutput.ReadToEndAsync(token);
            var stderr = process.StandardError.ReadToEndAsync(token);
            await process.WaitForExitAsync(token).ConfigureAwait(false);

            return new ToolRun(process.ExitCode, await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false));
        }, cancellationToken);

    private sealed record ToolRun(int ExitCode, string StdOut, string StdErr);
}
