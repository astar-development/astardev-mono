using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Ports;
using AStarDev.FunctionalParadigm;
using Microsoft.Extensions.DependencyInjection;

namespace AStarDev.CloudSync.Persistence.TestsUnit;

public sealed class AccountStoreShould
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task return_an_added_account()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var store = host.Services.GetRequiredService<IAccountStore>();
        var account = NewAccount("acct-1");

        var added = await store.AddAsync(account, Token);
        var found = await store.GetAsync(account.Id, Token);

        added.Value.ShouldBeOfType<Ok<Unit>>();
        found.Value.ShouldBeOfType<Ok<Account>>().Value.ShouldBe(account);
    }

    [Fact]
    public async Task report_an_unknown_account_as_not_found()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var store = host.Services.GetRequiredService<IAccountStore>();
        var id = new AccountId(Guid.NewGuid());

        var found = await store.GetAsync(id, Token);

        found.Value.ShouldBeOfType<Fail<StoreError>>().Error.Value.ShouldBe(new AccountNotFound(id));
    }

    [Fact]
    public async Task refuse_a_second_account_for_the_same_provider_account()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var store = host.Services.GetRequiredService<IAccountStore>();
        await store.AddAsync(NewAccount("acct-1"), Token);
        var duplicate = NewAccount("acct-1");

        var result = await store.AddAsync(duplicate, Token);

        result.Value.ShouldBeOfType<Fail<StoreError>>().Error.Value.ShouldBe(new AccountAlreadyExists(duplicate.Id));
    }

    [Fact]
    public async Task list_accounts_oldest_first()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var store = host.Services.GetRequiredService<IAccountStore>();
        var newer = NewAccount("newer") with { CreatedUtc = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero) };
        var older = NewAccount("older") with { CreatedUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) };
        await store.AddAsync(newer, Token);
        await store.AddAsync(older, Token);

        var listed = await store.ListAsync(Token);

        listed.Value.ShouldBeOfType<Ok<IReadOnlyList<Account>>>().Value.ShouldBe([older, newer]);
    }

    [Fact]
    public async Task keep_the_updated_status()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var store = host.Services.GetRequiredService<IAccountStore>();
        var account = NewAccount("acct-1");
        await store.AddAsync(account, Token);

        var updated = await store.UpdateStatusAsync(account.Id, AccountStatus.ReauthenticationRequired, Token);
        var found = await store.GetAsync(account.Id, Token);

        updated.Value.ShouldBeOfType<Ok<Unit>>();
        found.Value.ShouldBeOfType<Ok<Account>>().Value.ShouldBe(account with { Status = AccountStatus.ReauthenticationRequired });
    }

    [Fact]
    public async Task report_an_unknown_account_when_updating_the_status()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var store = host.Services.GetRequiredService<IAccountStore>();
        var id = new AccountId(Guid.NewGuid());

        var result = await store.UpdateStatusAsync(id, AccountStatus.Connected, Token);

        result.Value.ShouldBeOfType<Fail<StoreError>>().Error.Value.ShouldBe(new AccountNotFound(id));
    }

    [Fact]
    public async Task forget_a_removed_account()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var store = host.Services.GetRequiredService<IAccountStore>();
        var account = NewAccount("acct-1");
        await store.AddAsync(account, Token);

        var removed = await store.RemoveAsync(account.Id, Token);
        var found = await store.GetAsync(account.Id, Token);

        removed.Value.ShouldBeOfType<Ok<Unit>>();
        found.Value.ShouldBeOfType<Fail<StoreError>>().Error.Value.ShouldBe(new AccountNotFound(account.Id));
    }

    [Fact]
    public async Task report_an_unknown_account_when_removing()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var store = host.Services.GetRequiredService<IAccountStore>();
        var id = new AccountId(Guid.NewGuid());

        var result = await store.RemoveAsync(id, Token);

        result.Value.ShouldBeOfType<Fail<StoreError>>().Error.Value.ShouldBe(new AccountNotFound(id));
    }

    [Fact]
    public async Task report_storage_failure_when_the_database_has_no_schema()
    {
        await using var host = PersistenceTestHost.Create();
        var store = host.Services.GetRequiredService<IAccountStore>();

        var result = await store.AddAsync(NewAccount("acct-1"), Token);

        result.Value.ShouldBeOfType<Fail<StoreError>>().Error.Value.ShouldBeOfType<StorageFailed>();
    }

    [Fact]
    public async Task honour_cancellation()
    {
        await using var host = await PersistenceTestHost.CreateInitialisedAsync(Token);
        var store = host.Services.GetRequiredService<IAccountStore>();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => store.AddAsync(NewAccount("acct-1"), cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => store.GetAsync(new AccountId(Guid.NewGuid()), cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => store.ListAsync(cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => store.UpdateStatusAsync(new AccountId(Guid.NewGuid()), AccountStatus.Connected, cancelled.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => store.RemoveAsync(new AccountId(Guid.NewGuid()), cancelled.Token));
    }

    private static Account NewAccount(string identifier)
        => new(new AccountId(Guid.NewGuid()), "OneDrive", identifier, identifier, new DateTimeOffset(2026, 11, 21, 0, 0, 0, TimeSpan.Zero));
}
