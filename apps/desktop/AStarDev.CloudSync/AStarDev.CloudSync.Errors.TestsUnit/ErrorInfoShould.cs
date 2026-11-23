using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.Errors.TestsUnit;

public sealed class ErrorInfoShould
{
    [Fact]
    public void read_the_code_of_an_error_case() => ErrorInfo.CodeOf(new CodedError()).Value.ShouldBe("CS-TEST-0001");

    [Fact]
    public void return_the_default_code_for_a_value_without_one() => ErrorInfo.CodeOf("plain").ShouldBe(default);

    private sealed record CodedError : IErrorInfo
    {
        public ErrorCode Code => ErrorCode.Create("CS-TEST-0001").Value is Ok<ErrorCode> ok ? ok.Value : default;

        public string HelpLink => ErrorLinks.For(Code);
    }
}
