using AStarDev.CloudSync.Errors;
using Microsoft.Identity.Client;

namespace AStarDev.CloudSync.OneDrive.TestsUnit;

public sealed class MsalSilentSignInShould
{
    [Fact]
    public async Task set_the_help_link_when_the_account_is_unknown()
    {
        var signIn = new MsalSilentSignIn(new OneDriveOptions("client", OneDriveOptions.ConsumersAuthority));

        var thrown = await Should.ThrowAsync<MsalUiRequiredException>(() => signIn.AcquireAsync([], "unknown", TestContext.Current.CancellationToken));

        thrown.HelpLink.ShouldBe(ErrorLinks.Root + "CS-ONEDRIVE-0001");
    }
}
