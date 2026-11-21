using AStarDev.CloudSync.Ports;

namespace AStarDev.CloudSync.Daemon.TestsUnit;

public sealed class ProviderErrorMessagesShould
{
    [Fact]
    public void pass_an_authentication_message_through_unchanged()
        => ProviderErrorMessages.Describe(new AuthenticationFailed("Keyring missing.")).ShouldBe("Keyring missing.");

    [Fact]
    public void say_how_long_to_wait_when_rate_limited()
        => ProviderErrorMessages.Describe(new RateLimited(TimeSpan.FromSeconds(12))).ShouldContain("12 seconds");

    [Fact]
    public void advise_a_retry_for_a_transient_failure()
        => ProviderErrorMessages.Describe(new Transient("offline")).ShouldContain("try again", Case.Insensitive);

    [Fact]
    public void include_the_reason_for_a_permanent_failure()
        => ProviderErrorMessages.Describe(new Permanent("forbidden")).ShouldContain("forbidden");
}
