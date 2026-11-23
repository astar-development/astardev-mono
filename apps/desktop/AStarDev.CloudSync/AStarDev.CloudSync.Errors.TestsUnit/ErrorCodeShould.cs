using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Errors.TestsUnit;

public sealed class ErrorCodeShould
{
    [Theory]
    [InlineData("CS-IPC-0003")]
    [InlineData("CS-SYNCENGINE-0120")]
    [InlineData("CS-UI-9999")]
    public void accept_a_well_formed_code(string text)
    {
        var result = ErrorCode.Create(text);

        result.Value.ShouldBeOfType<Ok<ErrorCode>>().Value.Value.ShouldBe(text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("cs-ipc-0003")]
    [InlineData("CS-IPC-3")]
    [InlineData("CS-IPC-00031")]
    [InlineData("CS--0003")]
    [InlineData("CS-I-0003")]
    [InlineData("XX-IPC-0003")]
    [InlineData(" CS-IPC-0003")]
    [InlineData("CS-IPC-0003\n")]
    public void return_a_failure_for_a_malformed_code(string? text)
    {
        var result = ErrorCode.Create(text);

        result.Value.ShouldBeOfType<Fail<InvalidErrorCode>>().Error.Value.ShouldBe(text ?? string.Empty);
    }
}
