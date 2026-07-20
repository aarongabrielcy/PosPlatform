using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;

namespace Pos.Domain.Tests.Common.Identifiers;

public class RoleIdTests
{
    [Fact]
    public void NewGeneratesValueDistinctFromEmptyGuid()
    {
        var id = RoleId.New();

        Assert.NotEqual(Guid.Empty, id.Value);
    }

    [Fact]
    public void ConstructingWithEmptyGuidThrowsDomainValidationException()
    {
        Assert.Throws<DomainValidationException>(() => new RoleId(Guid.Empty));
    }

    [Fact]
    public void IdentifiersWithSameGuidAreEqual()
    {
        var guid = Guid.NewGuid();

        var first = new RoleId(guid);
        var second = new RoleId(guid);

        Assert.Equal(first, second);
    }

    [Fact]
    public void IdentifiersWithDifferentGuidAreNotEqual()
    {
        var first = new RoleId(Guid.NewGuid());
        var second = new RoleId(Guid.NewGuid());

        Assert.NotEqual(first, second);
    }
}
