using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;

namespace AStarDev.CloudSync.OneDrive.TestsUnit;

public sealed class OneDriveOptionsShould
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void require_a_client_id_and_use_the_consumers_authority(string? clientId)
    {
        var result = OneDriveOptions.Create(clientId);

        result.Value.ShouldBeOfType<Fail<ProviderError>>().Error.Value.ShouldBeOfType<AuthenticationFailed>();
    }

    [Fact]
    public void use_the_consumers_authority_for_a_configured_client_id()
    {
        var result = OneDriveOptions.Create("abc");

        var options = result.Value.ShouldBeOfType<Ok<OneDriveOptions>>().Value;
        options.ClientId.ShouldBe("abc");
        options.Authority.ShouldBe("https://login.microsoftonline.com/consumers");
    }
}
