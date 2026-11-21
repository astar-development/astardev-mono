using Microsoft.Identity.Client;

namespace AStarDev.CloudSync.OneDrive;

/// <summary>Builds the MSAL public client both sign-in paths share, with its token cache kept in memory between the load and the save.</summary>
internal sealed class MsalApplication
{
    private byte[] cache;

    private MsalApplication(IPublicClientApplication client, byte[] cache)
    {
        Client = client;
        this.cache = cache;
    }

    /// <summary>The MSAL client, whose token cache starts from the bytes given to <see cref="Create"/>.</summary>
    public IPublicClientApplication Client { get; }

    /// <summary>The token cache as MSAL has left it so far; the bytes given to <see cref="Create"/> until MSAL changes something.</summary>
    public byte[] Cache => cache;

    /// <summary>Creates a client for personal accounts that redirects to the loopback address and reads and writes its token cache through <see cref="Cache"/>.</summary>
    /// <param name="options">The app registration and authority.</param>
    /// <param name="initialCache">The cache as last saved; empty on first run.</param>
    public static MsalApplication Create(OneDriveOptions options, byte[] initialCache)
    {
        var client = PublicClientApplicationBuilder.Create(options.ClientId)
            .WithAuthority(options.Authority)
            .WithRedirectUri("http://localhost")
            .Build();
        var application = new MsalApplication(client, initialCache);
        client.UserTokenCache.SetBeforeAccess(args => args.TokenCache.DeserializeMsalV3(application.cache));
        client.UserTokenCache.SetAfterAccess(args =>
        {
            if (args.HasStateChanged) application.cache = args.TokenCache.SerializeMsalV3();
        });

        return application;
    }
}
