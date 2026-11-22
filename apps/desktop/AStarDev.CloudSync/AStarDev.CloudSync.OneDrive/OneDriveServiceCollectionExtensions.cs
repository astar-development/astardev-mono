using AStarDev.CloudSync.Ports;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.OneDrive;

/// <summary>Registers OneDrive as the cloud provider.</summary>
public static class OneDriveServiceCollectionExtensions
{
    private const string GraphBaseAddress = "https://graph.microsoft.com/v1.0/";

    /// <summary>
    /// Registers <see cref="ICloudProvider"/> backed by OneDrive, with its settings from the <c>OneDrive</c> configuration section, the Secret Service token cache,
    /// and a Graph client that sends the access token of the latest sign-in. Nothing contacts Microsoft until <see cref="ICloudProvider.ConnectAsync"/> runs.
    /// </summary>
    /// <param name="services">The collection to add the provider to.</param>
    /// <returns>The same collection, for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddOneDriveProvider();
    /// var result = await provider.GetRequiredService&lt;ICloudProvider&gt;().ConnectAsync(cancellationToken);
    /// </code>
    /// </example>
    public static IServiceCollection AddOneDriveProvider(this IServiceCollection services)
    {
        services.AddOptions<OneDriveSettings>().BindConfiguration(OneDriveSettings.ConfigurationSection);
        services.AddSingleton<OneDriveTelemetry>();
        services.AddSingleton<AccessTokenHolder>();
        services.AddSingleton<ITokenCacheStore>(_ => new SecretToolTokenCacheStore());
        services.AddSingleton(provider => new RootEnumerator(CreateGraphClient(provider.GetRequiredService<AccessTokenHolder>()), provider.GetRequiredService<OneDriveTelemetry>()));
        services.AddSingleton(provider => new ItemTransfer(CreateGraphClient(provider.GetRequiredService<AccessTokenHolder>()), new HttpClient(new SocketsHttpHandler()) { Timeout = Timeout.InfiniteTimeSpan }));
        services.AddSingleton<ICloudProvider, OneDriveProvider>();

        return services;
    }

    private static HttpClient CreateGraphClient(AccessTokenHolder holder)
        => new(new BearerTokenHandler(holder) { InnerHandler = new SocketsHttpHandler() }) { BaseAddress = new Uri(GraphBaseAddress) };
}
