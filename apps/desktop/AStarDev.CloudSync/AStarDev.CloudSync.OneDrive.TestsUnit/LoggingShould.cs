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
    private const string AccountId = "id";

    private const string SecretToken = "eyJ0b2tlbi1zZWNyZXQtMTIzNDU2";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task log_rate_limit_as_a_warning()
    {
        var logs = new CapturingLoggerProvider();
        using var provider = Create(logs, status: HttpStatusCode.TooManyRequests, retryAfter: TimeSpan.FromSeconds(7), token: null);

        var result = await provider.Value.GetSnapshotAsync(AccountId, new RemoteItemId("root-1"), null, Token);

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
        using var provider = Create(logs, status: HttpStatusCode.Unauthorized, retryAfter: null, token: null);

        await provider.Value.ListChildrenAsync(AccountId, new RemoteItemId("folder-1"), Token);

        var entry = logs.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Error);
        entry.EventId.ShouldBe(3001);
        entry.ScopeValue("RemoteItemId").ShouldBe("folder-1");
    }

    [Fact]
    public async Task not_log_tokens()
    {
        var logs = new CapturingLoggerProvider();
        using var provider = Create(logs, status: HttpStatusCode.Unauthorized, retryAfter: null, token: SecretToken);

        await provider.Value.GetSnapshotAsync(AccountId, new RemoteItemId("root-1"), null, Token);
        await provider.Value.GetDeltasAsync(AccountId, new RemoteItemId("root-1"), new DeltaToken("delta-link-secret"), null, Token);

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
        using var provider = Create(logs, status: HttpStatusCode.OK, retryAfter: null, token: null);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => provider.Value.ListChildrenAsync(AccountId, new RemoteItemId("folder-1"), cancelled.Token));
    }

    private static Owned Create(CapturingLoggerProvider logs, HttpStatusCode status, TimeSpan? retryAfter, string? token)
    {
        var telemetry = new OneDriveTelemetry();
        var handler = new FakeGraphHandler(_ =>
        {
            var response = FakeGraphHandler.Json("{}", status);
            if (retryAfter is { } delay) response.Headers.RetryAfter = new RetryConditionHeaderValue(delay);

            return response;
        });
        var clients = new List<HttpClient>();
        var accounts = new OneDriveAccounts(holder =>
        {
            var client = new HttpClient(new BearerTokenHandler(holder) { InnerHandler = handler }) { BaseAddress = new Uri("https://graph.test/v1.0/") };
            clients.Add(client);

            return new AccountClients(holder, new RootEnumerator(client, telemetry), new ItemTransfer(client, client));
        });
        if (token is not null) accounts.Set(AccountId, token);
        var factory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(logs));
        var provider = new OneDriveProvider(Options.Create(new OneDriveSettings()), new FakeTokenCacheStore(), accounts, TimeProvider.System, factory.CreateLogger<OneDriveProvider>());

        return new Owned(provider, telemetry, clients, factory);
    }

    private sealed record Owned(OneDriveProvider Value, OneDriveTelemetry Telemetry, IReadOnlyList<HttpClient> Clients, ILoggerFactory Factory) : IDisposable
    {
        public void Dispose()
        {
            foreach (var client in Clients) client.Dispose();
            Telemetry.Dispose();
            Factory.Dispose();
        }
    }
}
