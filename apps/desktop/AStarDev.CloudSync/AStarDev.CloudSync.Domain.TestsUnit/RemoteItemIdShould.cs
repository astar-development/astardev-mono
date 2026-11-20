namespace AStarDev.CloudSync.Domain.TestsUnit;

public sealed class RemoteItemIdShould
{
    [Fact]
    public void compare_by_value()
    {
        var first = new RemoteItemId("abc");
        var same = new RemoteItemId("abc");
        var other = new RemoteItemId("xyz");

        first.ShouldBe(same);
        (first == same).ShouldBeTrue();
        first.GetHashCode().ShouldBe(same.GetHashCode());
        first.ShouldNotBe(other);
    }
}
