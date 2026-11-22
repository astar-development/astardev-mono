using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.Localisation;
using CommunityToolkit.Mvvm.Input;

namespace AStarDev.CloudSync.UI.ViewModels.Screens;

/// <summary>One dead-lettered operation as text, with the Retry and Ignore buttons. Every value comes from the daemon's <see cref="DeadLetterView"/>; the only work done here is wording and formatting.</summary>
internal sealed class DeadLetterRowViewModel
{
    /// <summary>Words the operation for display and wires its buttons to the screen.</summary>
    /// <param name="view">What the daemon reported.</param>
    /// <param name="localisation">Supplies every text.</param>
    /// <param name="culture">Formats the date and time.</param>
    /// <param name="queue">Performs the decision when a button is pressed.</param>
    public DeadLetterRowViewModel(DeadLetterView view, ILocalisationService localisation, ICultureService culture, QueueViewModel queue)
    {
        OperationId = view.OperationId;
        TypeText = localisation.Get($"Queue.DeadLetters.Type.{view.Type}");
        ItemText = localisation.Format("Queue.DeadLetters.Item", ("id", view.ItemId));
        AttemptsText = localisation.Format("Queue.DeadLetters.Attempts", ("count", view.Attempts));
        ErrorText = view.LastError ?? localisation.Get("Queue.DeadLetters.NoError");
        DateText = culture.FormatDate(view.CreatedUtc);
        TimeText = culture.FormatTime(view.CreatedUtc);
        RetryCommand = new AsyncRelayCommand(cancellationToken => queue.RetryAsync(view.OperationId, cancellationToken));
        IgnoreCommand = new AsyncRelayCommand(cancellationToken => queue.IgnoreAsync(view.OperationId, cancellationToken));
    }

    /// <summary>The daemon's identity for the operation, as sent back in the decision.</summary>
    public Guid OperationId { get; }

    /// <summary>What the operation does, in words.</summary>
    public string TypeText { get; }

    /// <summary>The item acted on, by the daemon's identity; the daemon does not report the path.</summary>
    public string ItemText { get; }

    /// <summary>How many times the daemon tried before giving up.</summary>
    public string AttemptsText { get; }

    /// <summary>Why the last attempt failed, or a note that no reason was recorded.</summary>
    public string ErrorText { get; }

    /// <summary>The day the operation was queued, in the culture's format.</summary>
    public string DateText { get; }

    /// <summary>The time the operation was queued, in the culture's format.</summary>
    public string TimeText { get; }

    /// <summary>Asks the daemon to try the operation again.</summary>
    public IAsyncRelayCommand RetryCommand { get; }

    /// <summary>Asks the daemon to drop the operation.</summary>
    public IAsyncRelayCommand IgnoreCommand { get; }
}
