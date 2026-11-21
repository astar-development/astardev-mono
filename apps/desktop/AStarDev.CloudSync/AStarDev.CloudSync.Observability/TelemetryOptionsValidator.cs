using Microsoft.Extensions.Options;

namespace AStarDev.CloudSync.Observability;

internal sealed class TelemetryOptionsValidator : IValidateOptions<TelemetryOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, TelemetryOptions options)
        => string.IsNullOrEmpty(options.OtlpEndpoint) || IsHttpUri(options.OtlpEndpoint)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail($"{nameof(TelemetryOptions.OtlpEndpoint)} must be an absolute http or https URI.");

    private static bool IsHttpUri(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
