using System.Net;
using System.Text;

namespace AStarDev.CloudSync.TestsIntegration;

internal sealed class QueuedGraphHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> responses = new();

    public QueuedGraphHandler Then(HttpStatusCode status, string json = "{}")
    {
        responses.Enqueue(new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") });

        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(responses.Dequeue());
    }
}
