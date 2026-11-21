using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.IPC.TestsUnit;

internal static class ResultAssertions
{
    public static T ShouldSucceed<T>(this Result<T, IpcError> result)
        => result.Value.ShouldBeOfType<Ok<T>>().Value;

    public static IpcError ShouldFail<T>(this Result<T, IpcError> result)
        => result.Value.ShouldBeOfType<Fail<IpcError>>().Error;

    public static async Task ShouldBeCancelledAsync(Func<Task> operation)
    {
        var exception = await Record.ExceptionAsync(operation);

        exception.ShouldBeAssignableTo<OperationCanceledException>();
    }
}
