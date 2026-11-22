using System.IO.Abstractions;
using System.Net.Sockets;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;

namespace AStarDev.SingleInstance;

/// <summary>Claims the lock file and hands off to the running instance over a Unix domain socket.</summary>
/// <param name="logger">Reports hand-off results and lock failures, without paths.</param>
internal sealed class SingleInstanceGuard(ILogger<SingleInstanceGuard> logger) : ISingleInstanceGuard
{
    private const string Location = nameof(SingleInstanceGuard);
    private const string UnexpectedReply = "UnexpectedReply";

    private static readonly HashSet<int> SharingViolationCodes =
    [
        unchecked((int)0x80070020),
        unchecked((int)0x80070021),
        11,
        35,
    ];

    /// <inheritdoc />
    public Task<InstanceClaim> ClaimAsync(SingleInstanceOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fileSystem = options.DataFolder.FileSystem;
        string lockPath = fileSystem.Path.Join(options.DataFolder.FullName, options.Name + ".lock");
        var socketFile = fileSystem.FileInfo.New(fileSystem.Path.Join(options.DataFolder.FullName, options.Name + ".sock"));

        return Try.Run(() => CreateFolder(options.DataFolder))
            .Match(
                _ => Try.Run(() => (Stream)fileSystem.FileStream.New(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                    .Match(heldLock => Task.FromResult(BecomePrimary(heldLock, socketFile)), exception => HandOffOrFailAsync(exception, socketFile, options.HandOffTimeout, cancellationToken)),
                exception => Task.FromResult(LockUnavailable(exception)));
    }

    private static Unit CreateFolder(IDirectoryInfo folder)
    {
        if (OperatingSystem.IsWindows())
            folder.Create();
        else
            Directory.CreateDirectory(folder.FullName, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        return default;
    }

    private InstanceClaim BecomePrimary(Stream heldLock, IFileInfo socketFile)
    {
        _ = Try.Run(() => DeleteStaleSocketFile(socketFile));
        var listener = Listen(socketFile.FullName);
        _ = listener.Match(_ => default(Unit), LogHandOffUnavailable);

        return InstanceClaimFactory.CreatePrimary(PrimaryInstance.Start(heldLock, listener, socketFile, logger));
    }

    private static Unit DeleteStaleSocketFile(IFileInfo socketFile)
    {
        socketFile.Delete();

        return default;
    }

    private static Option<Socket> Listen(string socketPath) => Try.Run(() => HandOffProtocol.CreateListener(socketPath)).Match(Option.Some, _ => Option.None<Socket>());

    private Unit LogHandOffUnavailable()
    {
        logger.LogWarning("{Location}: the hand-off socket could not be opened; second launches will not be answered.", Location);

        return default;
    }

    private Task<InstanceClaim> HandOffOrFailAsync(Exception exception, IFileInfo socketFile, TimeSpan timeout, CancellationToken cancellationToken) =>
        IsHeldByAnotherInstance(exception)
            ? HandOffAsync(socketFile.FullName, timeout, cancellationToken)
            : Task.FromResult(LockUnavailable(exception));

    private static bool IsHeldByAnotherInstance(Exception exception) => exception.GetType() == typeof(IOException) && SharingViolationCodes.Contains(exception.HResult);

    private async Task<InstanceClaim> HandOffAsync(string socketPath, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        var answer = await Attempt.RunAsync(() => AskToShowAsync(socketPath, deadline.Token), deadline.Token).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        return answer.Match(reply => reply == HandOffProtocol.Acknowledgement ? HandedOff() : NotAnswered(UnexpectedReply), exception => NotAnswered(exception.GetType().Name));
    }

    private static async Task<string> AskToShowAsync(string socketPath, CancellationToken cancellationToken)
    {
        using var socket = HandOffProtocol.CreateSocket();
        await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), cancellationToken).ConfigureAwait(false);
        await HandOffProtocol.SendAsync(socket, HandOffProtocol.ShowRequest, cancellationToken).ConfigureAwait(false);

        return await HandOffProtocol.ReceiveAsync(socket, cancellationToken).ConfigureAwait(false);
    }

    private InstanceClaim HandedOff()
    {
        logger.LogInformation("{Location}: another instance is running and was asked to show its window.", Location);

        return InstanceClaimFactory.CreateHandedOff();
    }

    private InstanceClaim NotAnswered(string errorType)
    {
        logger.LogWarning("{Location}: another instance holds the lock but did not answer the hand-off ({ErrorType}).", Location, errorType);

        return InstanceClaimFactory.CreateNotAnswered();
    }

    private InstanceClaim LockUnavailable(Exception exception)
    {
        logger.LogError("{Location}: the single-instance lock could not be created ({ErrorType}).", Location, exception.GetType().Name);

        return InstanceClaimFactory.CreateLockUnavailable();
    }
}
