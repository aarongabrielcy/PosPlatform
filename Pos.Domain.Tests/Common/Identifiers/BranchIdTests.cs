using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;

namespace Pos.Domain.Tests.Common.Identifiers;

public class BranchIdTests
{
    [Fact]
    public void NewGeneratesValueDistinctFromEmptyGuid()
    {
        var id = BranchId.New();

        Assert.NotEqual(Guid.Empty, id.Value);
    }

    [Fact]
    public void ConstructingWithEmptyGuidThrowsDomainValidationException()
    {
        Assert.Throws<DomainValidationException>(() => new BranchId(Guid.Empty));
    }

    [Fact]
    public void IdentifiersWithSameGuidAreEqual()
    {
        var guid = Guid.NewGuid();

        var first = new BranchId(guid);
        var second = new BranchId(guid);

        Assert.Equal(first, second);
    }

    [Fact]
    public void IdentifiersWithDifferentGuidAreNotEqual()
    {
        var first = new BranchId(Guid.NewGuid());
        var second = new BranchId(Guid.NewGuid());

        Assert.NotEqual(first, second);
    }
}
