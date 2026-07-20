using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;

namespace Pos.Domain.Tests.Common.Identifiers;

public class RegisterIdTests
{
    [Fact]
    public void NewGeneratesValueDistinctFromEmptyGuid()
    {
        var id = RegisterId.New();

        Assert.NotEqual(Guid.Empty, id.Value);
    }

    [Fact]
    public void ConstructingWithEmptyGuidThrowsDomainValidationException()
    {
        Assert.Throws<DomainValidationException>(() => new RegisterId(Guid.Empty));
    }

    [Fact]
    public void IdentifiersWithSameGuidAreEqual()
    {
        var guid = Guid.NewGuid();

        var first = new RegisterId(guid);
        var second = new RegisterId(guid);

        Assert.Equal(first, second);
    }

    [Fact]
    public void IdentifiersWithDifferentGuidAreNotEqual()
    {
        var first = new RegisterId(Guid.NewGuid());
        var second = new RegisterId(Guid.NewGuid());

        Assert.NotEqual(first, second);
    }
}
