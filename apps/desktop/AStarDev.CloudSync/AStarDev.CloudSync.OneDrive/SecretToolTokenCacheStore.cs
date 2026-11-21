using System.Diagnostics;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.OneDrive;

/// <summary>Keeps the token cache in the freedesktop Secret Service through the <c>secret-tool</c> CLI. There is no file fallback: when the keyring is unavailable, the call fails.</summary>
internal sealed class SecretToolTokenCacheStore(string executable = "secret-tool") : ITokenCacheStore
{
    private const string Unavailable = "The Secret Service keyring is unavailable, so the OneDrive sign-in cannot be stored. Start a keyring (for example gnome-keyring or KWallet) and install libsecret's secret-tool. Tokens are never written to a file.";

    private static readonly string[] Attributes = ["service", "astardev-cloudsync", "account", "onedrive"];

    /// <inheritdoc/>
    /// <remarks>An entry that is not valid base64 is treated as nothing stored, so a corrupt cache only costs one fresh sign-in.</remarks>
    public async Task<Result<byte[], ProviderError>> LoadAsync(CancellationToken cancellationToken)
    {
        var run = await RunAsync(["lookup", .. Attributes], null, cancellationToken).ConfigureAwait(false);

        return run.ToResult(AuthenticationErrors.From) switch
        {
            Ok<ToolRun> { Value.ExitCode: 0 } ok => Decode(ok.Value.StdOut),
            Ok<ToolRun> { Value: { ExitCode: 1, StdErr: "" } } => new Ok<byte[]>([]),
            _ => new Fail<ProviderError>(new AuthenticationFailed(Unavailable))
        };
    }

    /// <inheritdoc/>
    public async Task<Result<Unit, ProviderError>> SaveAsync(byte[] cache, CancellationToken cancellationToken)
    {
        var run = await RunAsync(["store", "--label=AStarDev CloudSync OneDrive", .. Attributes], Convert.ToBase64String(cache), cancellationToken).ConfigureAwait(false);

        return run.ToResult(AuthenticationErrors.From) switch
        {
            Ok<ToolRun> { Value.ExitCode: 0 } => new Ok<Unit>(default),
            _ => new Fail<ProviderError>(new AuthenticationFailed(Unavailable))
        };
    }

    private static Result<byte[], ProviderError> Decode(string text)
        => Try.Run(() => Convert.FromBase64String(text.Trim())).ToResult(AuthenticationErrors.From) switch
        {
            Ok<byte[]> ok => ok,
            Fail<ProviderError> => new Ok<byte[]>([])
        };

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

            using var process = Process.Start(info) ?? throw new InvalidOperationException($"{executable} did not start.");
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
