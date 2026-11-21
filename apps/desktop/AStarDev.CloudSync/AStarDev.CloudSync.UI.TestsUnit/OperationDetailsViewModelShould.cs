using AStarDev.CloudSync.UI.Navigation;
using AStarDev.CloudSync.UI.ViewModels.Screens;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.UI.TestsUnit;

public sealed class OperationDetailsViewModelShould
{
    [Fact]
    public void open_without_navigating_away()
    {
        using var host = OperationScreensHost.Create();
        var navigation = host.Services.GetRequiredService<INavigationService>();
        _ = navigation.Navigate(ScreenId.Activity);
        var raised = 0;
        navigation.Navigated += (_, _) => raised++;
        var activity = host.Screen<ActivityViewModel>();

        activity.Details.Open(activity.Rows[0]);

        activity.Details.IsOpen.ShouldBeTrue();
        activity.Details.Row.ShouldBeSameAs(activity.Rows[0]);
        navigation.Current.ShouldBeSameAs(activity);
        raised.ShouldBe(0);
    }

    [Fact]
    public void close_and_stay_closed_when_closed_twice()
    {
        using var host = OperationScreensHost.Create();
        var activity = host.Screen<ActivityViewModel>();
        activity.Details.Open(activity.Rows[0]);

        activity.Details.Close();
        activity.Details.Close();

        activity.Details.IsOpen.ShouldBeFalse();
        activity.Details.Row.ShouldBeNull();
    }
}
