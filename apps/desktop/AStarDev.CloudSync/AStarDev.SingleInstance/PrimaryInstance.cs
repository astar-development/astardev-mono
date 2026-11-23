using System.IO.Abstractions;
using System.Net.Sockets;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;

namespace AStarDev.SingleInstance;

/// <summary>The running instance: holds the lock and listens for second launches until disposed.</summary>
public sealed class PrimaryInstance : IAsyncDisposable
{
    private const string Location = nameof(PrimaryInstance);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(100);
    private const int MaxConsecutiveAcceptFailures = 5;

    private readonly Stream heldLock;
    private readonly Option<Socket> listener;
    private readonly IFileInfo socketFile;
    private readonly ILogger logger;
    private readonly CancellationTokenSource stopping = new();
    private readonly Task listening;

    private PrimaryInstance(Stream heldLock, Option<Socket> listener, IFileInfo socketFile, ILogger logger)
    {
        this.heldLock = heldLock;
        this.listener = listener;
        this.socketFile = socketFile;
        this.logger = logger;
        listening = listener.Match(socket => Task.Run(() => ListenAsync(socket, stopping.Token), stopping.Token), () => Task.CompletedTask);
    }

    /// <summary>Starts listening on <paramref name="listener"/>; the instance owns the lock, the listener and the socket file from now on.</summary>
    internal static PrimaryInstance Start(Stream heldLock, Option<Socket> listener, IFileInfo socketFile, ILogger logger) => new(heldLock, listener, socketFile, logger);

    /// <summary>Raised on a thread-pool thread when a second launch asks this instance to show its window.</summary>
    public event EventHandler? ActivationRequested;

    /// <summary>Stops listening, removes the socket file and releases the lock.</summary>
    public async ValueTask DisposeAsync()
    {
        await stopping.CancelAsync().ConfigureAwait(false);
        _ = listener.Match(CloseListener, () => default(Unit));
        await listening.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        _ = Try.Run(() => DeleteSocketFile(socketFile));
        await heldLock.DisposeAsync().ConfigureAwait(false);
        stopping.Dispose();
    }

    private static Unit DeleteSocketFile(IFileInfo file)
    {
        file.Delete();

        return default;
    }

    private static Unit CloseListener(Socket socket)
    {
        socket.Dispose();

        return default;
    }

    private async Task ListenAsync(Socket socket, CancellationToken cancellationToken)
    {
        int failures = 0;
        while (failures < MaxConsecutiveAcceptFailures && !cancellationToken.IsCancellationRequested)
        {
            var accepted = await Attempt.RunAsync(() => socket.AcceptAsync(cancellationToken).AsTask(), cancellationToken).ConfigureAwait(false);
            bool succeeded = await accepted.Match(client => AnswerAsync(client, cancellationToken), exception => AcceptFailedAsync(exception, cancellationToken)).ConfigureAwait(false);
            failures = succeeded ? 0 : failures + 1;
        }

        if (failures >= MaxConsecutiveAcceptFailures)
            SingleInstanceLog.AcceptingStopped(logger, Location, failures);
    }

    private async Task<bool> AcceptFailedAsync(Exception exception, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return false;

        SingleInstanceLog.AcceptFailed(logger, Location, exception.GetType().Name);
        await Task.Delay(RetryDelay, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

        return false;
    }

    private async Task<bool> AnswerAsync(Socket client, CancellationToken cancellationToken)
    {
        using (client)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(RequestTimeout);
            var request = await Attempt.RunAsync(() => HandOffProtocol.ReceiveAsync(client, timeout.Token), timeout.Token).ConfigureAwait(false);

            if (request.Match(message => message == HandOffProtocol.ShowRequest, _ => false))
            {
                _ = Try.Run(RaiseActivationRequested).Match(_ => default(Unit), ActivationHandlerFailed);
                _ = await Attempt.RunAsync(() => SendAcknowledgementAsync(client, timeout.Token), timeout.Token).ConfigureAwait(false);
            }
        }

        return true;
    }

    private Unit ActivationHandlerFailed(Exception exception)
    {
        SingleInstanceLog.ActivationHandlerFailed(logger, Location, exception.GetType().Name);

        return default;
    }

    private Unit RaiseActivationRequested()
    {
        ActivationRequested?.Invoke(this, EventArgs.Empty);

        return default;
    }

    private static async Task<Unit> SendAcknowledgementAsync(Socket client, CancellationToken cancellationToken)
    {
        await HandOffProtocol.SendAsync(client, HandOffProtocol.Acknowledgement, cancellationToken).ConfigureAwait(false);

        return default;
    }
}
