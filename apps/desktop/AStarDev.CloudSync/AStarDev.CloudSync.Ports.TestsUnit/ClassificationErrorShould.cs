using AStarDev.CloudSync.Domain;
using AStarDev.CloudSync.Errors;

namespace AStarDev.CloudSync.Ports.TestsUnit;

public sealed class ClassificationErrorShould
{
    [Fact]
    public void expose_a_unique_code_and_link_for_every_case()
    {
        var category = new CategoryId(Guid.NewGuid());
        IErrorInfo[] cases =
        [
            new CategoryNotFound(category),
            new CategoryAlreadyExists("Colour"),
            new CategoryNotEmpty(category),
            new TermNotFound(new TermId(Guid.NewGuid())),
            new TermAlreadyExists("red", category),
            new ItemNotFound(new ItemId(Guid.NewGuid())),
            new StorageFailed("locked")
        ];

        cases.ShouldExposeUniqueCodesAndLinks();
    }

    [Fact]
    public void not_share_a_code_with_a_store_error()
    {
        IErrorInfo[] classification = [new CategoryNotFound(default), new TermNotFound(default), new ItemNotFound(default)];
        IErrorInfo[] store = [new AccountNotFound(default), new UnknownSetting("x")];

        classification.Concat(store).ShouldExposeUniqueCodesAndLinks();
    }
}
