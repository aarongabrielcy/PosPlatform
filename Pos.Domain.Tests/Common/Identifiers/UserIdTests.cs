using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;

namespace Pos.Domain.Tests.Common.Identifiers;

public class UserIdTests
{
    [Fact]
    public void NewGeneratesValueDistinctFromEmptyGuid()
    {
        var id = UserId.New();

        Assert.NotEqual(Guid.Empty, id.Value);
    }

    [Fact]
    public void ConstructingWithEmptyGuidThrowsDomainValidationException()
    {
        Assert.Throws<DomainValidationException>(() => new UserId(Guid.Empty));
    }

    [Fact]
    public void IdentifiersWithSameGuidAreEqual()
    {
        var guid = Guid.NewGuid();

        var first = new UserId(guid);
        var second = new UserId(guid);

        Assert.Equal(first, second);
    }

    [Fact]
    public void IdentifiersWithDifferentGuidAreNotEqual()
    {
        var first = new UserId(Guid.NewGuid());
        var second = new UserId(Guid.NewGuid());

        Assert.NotEqual(first, second);
    }
}
