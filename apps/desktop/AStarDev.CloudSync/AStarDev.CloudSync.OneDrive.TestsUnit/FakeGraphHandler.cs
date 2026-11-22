using System.Net;
using System.Text;

namespace AStarDev.CloudSync.OneDrive.TestsUnit;

internal sealed class FakeGraphHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<Uri> Requests { get; } = [];

    public List<(HttpMethod Method, Uri Uri, string Body, string? ContentRange)> Calls { get; } = [];

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request.RequestUri!);
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        Calls.Add((request.Method, request.RequestUri!, body, request.Content?.Headers.ContentRange?.ToString()));

        return respond(request);
    }
}
