using AStarDev.CloudSync.Errors;

namespace AStarDev.CloudSync.Ports.TestsUnit;

public sealed class ProviderErrorShould
{
    [Fact]
    public void expose_a_code_and_link_for_every_case()
    {
        IErrorInfo[] cases =
        [
            new RateLimited(TimeSpan.FromSeconds(5)),
            new Transient("timeout"),
            new AuthenticationFailed("token expired"),
            new Permanent("access denied"),
            new InvalidDeltaToken("expired")
        ];

        cases.ShouldExposeUniqueCodesAndLinks();
    }
}
