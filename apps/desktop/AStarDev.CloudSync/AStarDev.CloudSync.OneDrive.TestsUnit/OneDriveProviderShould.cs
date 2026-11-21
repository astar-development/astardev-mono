using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Options;

namespace AStarDev.CloudSync.OneDrive.TestsUnit;

public sealed class OneDriveProviderShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task fail_to_connect_with_a_clear_message_when_no_client_id_is_configured()
    {
        using var telemetry = new OneDriveTelemetry();
        var holder = new AccessTokenHolder();
        using var client = new HttpClient(new FakeGraphHandler(_ => FakeGraphHandler.Json("{}")));
        var provider = new OneDriveProvider(Options.Create(new OneDriveSettings()), new UnusedStore(), holder, new RootEnumerator(client, telemetry));

        var result = await provider.ConnectAsync(Token);

        var error = result.Value.ShouldBeOfType<Fail<ProviderError>>().Error.Value.ShouldBeOfType<AuthenticationFailed>();
        error.Message.ShouldContain("client id");
        holder.Current.ShouldBeNull();
    }

    [Fact]
    public async Task list_the_drive_root_through_graph()
    {
        using var telemetry = new OneDriveTelemetry();
        var handler = new FakeGraphHandler(_ => FakeGraphHandler.Json("""{ "value": [ { "id": "a", "name": "a.txt", "size": 1, "lastModifiedDateTime": "2026-01-01T00:00:00Z", "file": {}, "parentReference": { "path": "/drive/root:" } } ] }"""));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://graph.test/v1.0/") };
        var provider = new OneDriveProvider(Options.Create(new OneDriveSettings()), new UnusedStore(), new AccessTokenHolder(), new RootEnumerator(client, telemetry));

        var result = await provider.GetSnapshotAsync(new RemoteItemId("root"), Token);

        result.Value.ShouldBeOfType<Ok<ProviderSnapshotPage>>().Value.Items.Count.ShouldBe(1);
    }

    [Fact]
    public async Task honour_cancellation_when_connecting()
    {
        using var telemetry = new OneDriveTelemetry();
        using var client = new HttpClient(new FakeGraphHandler(_ => FakeGraphHandler.Json("{}")));
        var provider = new OneDriveProvider(Options.Create(new OneDriveSettings { ClientId = "id" }), new UnusedStore(), new AccessTokenHolder(), new RootEnumerator(client, telemetry));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => provider.ConnectAsync(cancelled.Token));
    }

    [Fact]
    public async Task fail_to_reconnect_with_a_clear_message_when_no_client_id_is_configured()
    {
        using var telemetry = new OneDriveTelemetry();
        using var client = new HttpClient(new FakeGraphHandler(_ => FakeGraphHandler.Json("{}")));
        var provider = new OneDriveProvider(Options.Create(new OneDriveSettings()), new UnusedStore(), new AccessTokenHolder(), new RootEnumerator(client, telemetry));

        var result = await provider.ReconnectAsync("id", Token);

        result.Value.ShouldBeOfType<Fail<ProviderError>>().Error.Value.ShouldBeOfType<AuthenticationFailed>().Message.ShouldContain("client id");
    }

    [Fact]
    public async Task forget_the_token_when_disconnecting()
    {
        using var telemetry = new OneDriveTelemetry();
        var holder = new AccessTokenHolder();
        holder.Set("token");
        using var client = new HttpClient(new FakeGraphHandler(_ => FakeGraphHandler.Json("{}")));
        var provider = new OneDriveProvider(Options.Create(new OneDriveSettings()), new UnusedStore(), holder, new RootEnumerator(client, telemetry));

        var result = await provider.DisconnectAsync("id", Token);

        result.Value.ShouldBeOfType<Ok<Unit>>();
        holder.Current.ShouldBeNull();
    }

    [Fact]
    public async Task honour_cancellation_when_disconnecting()
    {
        using var telemetry = new OneDriveTelemetry();
        using var client = new HttpClient(new FakeGraphHandler(_ => FakeGraphHandler.Json("{}")));
        var provider = new OneDriveProvider(Options.Create(new OneDriveSettings()), new UnusedStore(), new AccessTokenHolder(), new RootEnumerator(client, telemetry));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => provider.DisconnectAsync("id", cancelled.Token));
    }

    private sealed class UnusedStore : ITokenCacheStore
    {
        public Task<Result<byte[], ProviderError>> LoadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<Result<byte[], ProviderError>>(new Ok<byte[]>([]));
        }

        public Task<Result<Unit, ProviderError>> ClearAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<Result<Unit, ProviderError>>(new Ok<Unit>(default));
        }

        public Task<Result<Unit, ProviderError>> SaveAsync(byte[] cache, CancellationToken cancellationToken)
            => Task.FromResult<Result<Unit, ProviderError>>(new Ok<Unit>(default));
    }
}
