namespace AStarDev.CloudSync.OneDrive.TestsUnit;

public sealed class BearerTokenHandlerShould
{
    [Fact]
    public async Task send_the_access_token_as_a_bearer_header()
    {
        var holder = new AccessTokenHolder();
        holder.Set("abc");
        HttpRequestMessage? seen = null;
        using var client = new HttpClient(new BearerTokenHandler(holder) { InnerHandler = new FakeGraphHandler(request => { seen = request; return FakeGraphHandler.Json("{}"); }) });

        await client.GetAsync("https://graph.test/x", TestContext.Current.CancellationToken);

        seen!.Headers.Authorization!.Scheme.ShouldBe("Bearer");
        seen.Headers.Authorization.Parameter.ShouldBe("abc");
    }

    [Fact]
    public async Task send_no_authorization_header_before_sign_in()
    {
        HttpRequestMessage? seen = null;
        using var client = new HttpClient(new BearerTokenHandler(new AccessTokenHolder()) { InnerHandler = new FakeGraphHandler(request => { seen = request; return FakeGraphHandler.Json("{}"); }) });

        await client.GetAsync("https://graph.test/x", TestContext.Current.CancellationToken);

        seen!.Headers.Authorization.ShouldBeNull();
    }

    [Fact]
    public async Task honour_cancellation()
    {
        using var client = new HttpClient(new BearerTokenHandler(new AccessTokenHolder()) { InnerHandler = new FakeGraphHandler(_ => FakeGraphHandler.Json("{}")) });
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => client.GetAsync("https://graph.test/x", cancelled.Token));
    }
}
