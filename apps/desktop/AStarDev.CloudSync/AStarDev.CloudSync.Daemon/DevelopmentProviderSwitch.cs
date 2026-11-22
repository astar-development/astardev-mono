#if DEBUG
using AStarDev.CloudSync.FakeProvider;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AStarDev.CloudSync.Daemon;

/// <summary>Lets a debug build of the daemon run against the fake provider, so crash-recovery checks need no OneDrive account.</summary>
internal static class DevelopmentProviderSwitch
{
    private const string RealProviderKey = "real";
    private const int FakePageSize = 1000;

    /// <summary>
    /// Wraps the <see cref="ICloudProvider"/> registered so far: with <c>CloudSync:Provider=Fake</c> the daemon gets a <see cref="FakeCloudProvider"/> holding a generated tree of <c>CloudSync:FakeTreeSize</c> items,
    /// otherwise the original registration. Call it after the real provider is registered. A registration that is not a plain implementation type is left untouched.
    /// </summary>
    /// <param name="services">The collection that already holds the real provider.</param>
    public static IServiceCollection AddDevelopmentProviderSwitch(this IServiceCollection services)
    {
        services.AddOptions<DevelopmentProviderOptions>().BindConfiguration(ApplicationConstants.DevelopmentConfigurationSection);
        var real = services.Last(descriptor => descriptor.ServiceType == typeof(ICloudProvider));

        return Option.FromNullable(real.ImplementationType).Match(type => Wrap(services, real, type), () => services);
    }

    private static IServiceCollection Wrap(IServiceCollection services, ServiceDescriptor real, Type implementation)
    {
        services.Remove(real);
        services.Add(ServiceDescriptor.DescribeKeyed(typeof(ICloudProvider), RealProviderKey, implementation, real.Lifetime));
        services.AddSingleton(Choose);

        return services;
    }

    private static ICloudProvider Choose(IServiceProvider provider)
        => provider.GetRequiredService<IOptions<DevelopmentProviderOptions>>().Value is { Provider: DevelopmentProviderKind.Fake } options
            ? CreateFake(options.FakeTreeSize)
            : provider.GetRequiredKeyedService<ICloudProvider>(RealProviderKey);

    private static FakeCloudProvider CreateFake(int treeSize)
    {
        var fake = new FakeCloudProvider(FakePageSize) { ServeGeneratedContent = true };
        fake.GenerateTree(treeSize);

        return fake;
    }
}
#endif
