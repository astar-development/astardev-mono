using System.Net;
using System.Net.Http.Headers;
using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AStarDev.CloudSync.OneDrive.TestsUnit;

public sealed class LoggingShould
{
    private const string SecretToken = "eyJ0b2tlbi1zZWNyZXQtMTIzNDU2";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task log_rate_limit_as_a_warning()
    {
        var logs = new CapturingLoggerProvider();
        using var provider = Create(logs, status: HttpStatusCode.TooManyRequests, retryAfter: TimeSpan.FromSeconds(7), holder: new AccessTokenHolder());

        var result = await provider.Value.GetSnapshotAsync(new RemoteItemId("root-1"), null, Token);

        result.Value.ShouldBeOfType<Fail<ProviderError>>().Error.Value.ShouldBeOfType<RateLimited>();
        var entry = logs.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.EventId.ShouldBe(3000);
        entry.State["RetryAfterSeconds"].ShouldBe(7d);
        entry.ScopeValue("RemoteItemId").ShouldBe("root-1");
    }

    [Fact]
    public async Task log_an_expired_authentication_as_an_error_with_the_account_id()
    {
        var logs = new CapturingLoggerProvider();
        using var provider = Create(logs, status: HttpStatusCode.Unauthorized, retryAfter: null, holder: new AccessTokenHolder());

        await provider.Value.ListChildrenAsync(new RemoteItemId("folder-1"), Token);

        var entry = logs.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Error);
        entry.EventId.ShouldBe(3001);
        entry.ScopeValue("RemoteItemId").ShouldBe("folder-1");
    }

    [Fact]
    public async Task not_log_tokens()
    {
        var logs = new CapturingLoggerProvider();
        var holder = new AccessTokenHolder();
        holder.Set(SecretToken);
        using var provider = Create(logs, status: HttpStatusCode.Unauthorized, retryAfter: null, holder: holder);

        await provider.Value.GetSnapshotAsync(new RemoteItemId("root-1"), null, Token);
        await provider.Value.GetDeltasAsync(new RemoteItemId("root-1"), new DeltaToken("delta-link-secret"), null, Token);

        logs.Entries.ShouldNotBeEmpty();
        foreach (var entry in logs.Entries)
        {
            entry.Message.ShouldNotContain(SecretToken);
            entry.State.Values.Concat(entry.Scopes.SelectMany(scope => scope.Values)).Select(value => value?.ToString()).ShouldAllBe(value => value == null || !value.Contains(SecretToken));
        }
    }

    [Fact]
    public async Task honour_cancellation_when_observing_a_provider_call()
    {
        var logs = new CapturingLoggerProvider();
        using var provider = Create(logs, status: HttpStatusCode.OK, retryAfter: null, holder: new AccessTokenHolder());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => provider.Value.ListChildrenAsync(new RemoteItemId("folder-1"), cancelled.Token));
    }

    private static Owned Create(CapturingLoggerProvider logs, HttpStatusCode status, TimeSpan? retryAfter, AccessTokenHolder holder)
    {
        var telemetry = new OneDriveTelemetry();
        var handler = new FakeGraphHandler(_ =>
        {
            var response = FakeGraphHandler.Json("{}", status);
            if (retryAfter is { } delay) response.Headers.RetryAfter = new RetryConditionHeaderValue(delay);

            return response;
        });
        var client = new HttpClient(new BearerTokenHandler(holder) { InnerHandler = handler }) { BaseAddress = new Uri("https://graph.test/v1.0/") };
        var factory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(logs));
        var provider = new OneDriveProvider(Options.Create(new OneDriveSettings()), new FakeTokenCacheStore(), holder, new RootEnumerator(client, telemetry), new ItemTransfer(client, client), factory.CreateLogger<OneDriveProvider>());

        return new Owned(provider, telemetry, client, factory);
    }

    private sealed record Owned(OneDriveProvider Value, OneDriveTelemetry Telemetry, HttpClient Client, ILoggerFactory Factory) : IDisposable
    {
        public void Dispose()
        {
            Client.Dispose();
            Telemetry.Dispose();
            Factory.Dispose();
        }
    }
}
