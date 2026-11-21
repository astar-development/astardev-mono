using AStarDev.CloudSync.Observability;
using AStarDev.CloudSync.UI.Localisation;
using AStarDev.CloudSync.UI.Theming;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.UI;

/// <summary>The UI's whole service graph in one place, so the application and the tests compose exactly the same services.</summary>
internal static class UiServiceCollectionExtensions
{
    /// <summary>
    /// Registers telemetry, so the UI exports its IPC spans, metrics and logs to the <c>OpenTelemetry:OtlpEndpoint</c> setting the daemon uses. The UI
    /// span of a request and the daemon span it caused then share one trace. An empty endpoint exports nothing, and a malformed one fails the host when it starts.
    /// </summary>
    /// <example>
    /// <code>
    /// var builder = Host.CreateApplicationBuilder();
    /// builder.Services.AddCloudSyncUi();
    /// await builder.Build().StartAsync(cancellationToken);
    /// </code>
    /// </example>
    public static IServiceCollection AddCloudSyncUi(this IServiceCollection services)
    {
        services.AddCloudSyncTelemetry(ApplicationConstants.ServiceName);
        services.AddSingleton<ICultureService>(new CultureService(LocalisationService.DefaultLanguage));
        services.AddSingleton<ILocalisationService>(provider => CreateLocalisation(provider.GetRequiredService<ICultureService>()));
        services.AddSingleton<IThemeService, ThemeService>();

        return services;
    }

    private static LocalisationService CreateLocalisation(ICultureService culture) => LocalisationCatalogue.Load(typeof(App).Assembly, LocalisationService.DefaultLanguage) switch
    {
        Ok<IReadOnlyDictionary<string, string>> ok => new LocalisationService(culture, new Dictionary<string, IReadOnlyDictionary<string, string>> { [LocalisationService.DefaultLanguage] = ok.Value }, LocalisationService.DefaultLanguage),
        Fail<LocalisationError> fail => throw new InvalidOperationException($"The embedded localisation resources are invalid: {fail.Error.Value}")
    };
}
