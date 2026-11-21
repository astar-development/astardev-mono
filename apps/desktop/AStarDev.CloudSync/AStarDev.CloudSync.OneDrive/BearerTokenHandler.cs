using System.Net.Http.Headers;

namespace AStarDev.CloudSync.OneDrive;

/// <summary>Adds the held access token to every outgoing request as a bearer header.</summary>
internal sealed class BearerTokenHandler(AccessTokenHolder holder) : DelegatingHandler
{
    private const string BearerScheme = "Bearer";

    /// <inheritdoc/>
    /// <remarks>Without a token the request goes out unauthenticated, so Graph answers 401 and the caller gets an <see cref="Ports.AuthenticationFailed"/> error.</remarks>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (holder.Current is { } token) request.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, token);

        return base.SendAsync(request, cancellationToken);
    }
}
