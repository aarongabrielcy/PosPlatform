using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;

namespace Pos.Domain.Tests.Common.Identifiers;

public class OrganizationIdTests
{
    [Fact]
    public void NewGeneratesValueDistinctFromEmptyGuid()
    {
        var id = OrganizationId.New();

        Assert.NotEqual(Guid.Empty, id.Value);
    }

    [Fact]
    public void ConstructingWithEmptyGuidThrowsDomainValidationException()
    {
        Assert.Throws<DomainValidationException>(() => new OrganizationId(Guid.Empty));
    }

    [Fact]
    public void IdentifiersWithSameGuidAreEqual()
    {
        var guid = Guid.NewGuid();

        var first = new OrganizationId(guid);
        var second = new OrganizationId(guid);

        Assert.Equal(first, second);
    }

    [Fact]
    public void IdentifiersWithDifferentGuidAreNotEqual()
    {
        var first = new OrganizationId(Guid.NewGuid());
        var second = new OrganizationId(Guid.NewGuid());

        Assert.NotEqual(first, second);
    }
}
