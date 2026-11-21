using AStarDev.CloudSync.Ports;
using Microsoft.Identity.Client;

namespace AStarDev.CloudSync.OneDrive.TestsUnit;

public sealed class AuthenticationErrorsShould
{
    [Fact]
    public void map_msal_failures_to_authentication_failure()
    {
        var error = AuthenticationErrors.From(new MsalServiceException("invalid_grant", "refused"));

        error.Value.ShouldBeOfType<AuthenticationFailed>().Message.ShouldContain("refused");
    }

    [Fact]
    public void map_network_failures_to_transient()
    {
        var error = AuthenticationErrors.From(new HttpRequestException("down"));

        error.Value.ShouldBeOfType<Transient>();
    }

    [Fact]
    public void map_anything_else_to_permanent()
    {
        var error = AuthenticationErrors.From(new InvalidOperationException("odd"));

        error.Value.ShouldBeOfType<Permanent>();
    }
}
