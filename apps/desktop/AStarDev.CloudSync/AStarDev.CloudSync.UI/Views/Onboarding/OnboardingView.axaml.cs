using Avalonia.Controls;

namespace AStarDev.CloudSync.UI.Views.Onboarding;

/// <summary>The first-run flow drawn over the whole window: one card showing the current step, with Back, Continue and, on the review, Confirm.</summary>
public partial class OnboardingView : UserControl
{
    /// <summary>Builds the controls declared in <c>OnboardingView.axaml</c>.</summary>
    public OnboardingView()
    {
        InitializeComponent();
    }
}
