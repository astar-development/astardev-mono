using System.Net;
using AStarDev.CloudSync.Ports;

namespace AStarDev.CloudSync.OneDrive;

/// <summary>Translates failed Graph HTTP responses into <see cref="ProviderError"/>.</summary>
internal static class GraphErrors
{
    /// <summary>Wait applied when Graph answers 429 without a usable Retry-After header.</summary>
    public static readonly TimeSpan DefaultRetryAfter = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 429 is <see cref="RateLimited"/> carrying the Retry-After delay (or <see cref="DefaultRetryAfter"/> when absent),
    /// 5xx and 408 are <see cref="Transient"/>, 401 is <see cref="AuthenticationFailed"/>, 410 is <see cref="InvalidDeltaToken"/> (Graph's answer to a spent delta link), everything else including 403 and 404 is <see cref="Permanent"/>.
    /// </summary>
    public static ProviderError From(HttpResponseMessage response)
    {
        var message = $"Graph returned {(int)response.StatusCode} {response.ReasonPhrase}".TrimEnd();

        return response.StatusCode switch
        {
            HttpStatusCode.TooManyRequests => new RateLimited(RetryAfterOf(response)),
            HttpStatusCode.RequestTimeout => new Transient(message),
            >= HttpStatusCode.InternalServerError => new Transient(message),
            HttpStatusCode.Unauthorized => new AuthenticationFailed(message),
            HttpStatusCode.Gone => new InvalidDeltaToken(message),
            _ => new Permanent(message)
        };
    }

    private static TimeSpan RetryAfterOf(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        var delay = retryAfter?.Delta ?? (retryAfter?.Date - DateTimeOffset.UtcNow);

        return delay is { } known && known >= TimeSpan.Zero ? known : DefaultRetryAfter;
    }
}
