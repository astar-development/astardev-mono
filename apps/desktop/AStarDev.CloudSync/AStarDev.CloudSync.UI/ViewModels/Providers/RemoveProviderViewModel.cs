using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AStarDev.CloudSync.UI.ViewModels.Providers;

/// <summary>The removal flow for one account: disconnect without deleting anything, only after the user explicitly confirms.</summary>
/// <param name="title">Names the account being removed.</param>
/// <param name="disconnect">Asks the daemon to forget the account.</param>
/// <param name="close">Closes the flow, called after a confirmed removal and on cancel.</param>
/// <param name="keepsFilesText">Says that local and remote files are kept.</param>
/// <param name="recoveryText">Says how to connect the account again.</param>
internal sealed partial class RemoveProviderViewModel(string title, Func<CancellationToken, Task> disconnect, Action close, string keepsFilesText, string recoveryText) : ViewModelBase
{
    /// <summary>Names the account being removed.</summary>
    public string Title => title;

    /// <summary>Says that no file is deleted by removing the account.</summary>
    public string KeepsFilesText => keepsFilesText;

    /// <summary>Says how to get the account back.</summary>
    public string RecoveryText => recoveryText;

    /// <summary>True once the user has ticked the confirmation. Removal does nothing while it is false.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial bool IsConfirmed { get; set; }

    /// <summary>
    /// Removes the account when <see cref="IsConfirmed"/> is true, otherwise does nothing. Cancellation surfaces as <see cref="OperationCanceledException"/>.
    /// Also exposed to the view as <c>ConfirmCommand</c>, which is disabled until confirmed.
    /// </summary>
    [RelayCommand(CanExecute = nameof(IsConfirmed))]
    public async Task ConfirmAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsConfirmed) return;

        await disconnect(cancellationToken);
        close();
    }

    /// <summary>Closes the flow and leaves the account connected. Also exposed to the view as <c>CancelCommand</c>.</summary>
    [RelayCommand]
    public void Cancel() => close();
}
