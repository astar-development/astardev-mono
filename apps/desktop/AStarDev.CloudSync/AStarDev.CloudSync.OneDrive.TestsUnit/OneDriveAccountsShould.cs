namespace AStarDev.CloudSync.OneDrive.TestsUnit;

public sealed class OneDriveAccountsShould
{
    [Fact]
    public void keep_a_separate_token_for_each_account()
    {
        var accounts = TestAccounts.Create();

        accounts.Set("one", "token-one");
        accounts.Set("two", "token-two");

        accounts.For("one").Holder.Current.ShouldBe("token-one");
        accounts.For("two").Holder.Current.ShouldBe("token-two");
    }

    [Fact]
    public void leave_an_unknown_account_without_a_token()
        => TestAccounts.Create().For("never-signed-in").Holder.Current.ShouldBeNull();

    [Fact]
    public void forget_only_the_cleared_account()
    {
        var accounts = TestAccounts.Create();
        accounts.Set("one", "token-one");
        accounts.Set("two", "token-two");

        accounts.Clear("one");

        accounts.For("one").Holder.Current.ShouldBeNull();
        accounts.For("two").Holder.Current.ShouldBe("token-two");
    }

    [Fact]
    public void clear_an_account_that_never_signed_in_without_failing()
        => Should.NotThrow(() => TestAccounts.Create().Clear("never-signed-in"));
}
