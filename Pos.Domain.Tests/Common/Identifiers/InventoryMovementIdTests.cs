using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;

namespace Pos.Domain.Tests.Common.Identifiers;

public class InventoryMovementIdTests
{
    [Fact]
    public void NewGeneratesValueDifferentFromEmpty()
    {
        var id = InventoryMovementId.New();

        Assert.NotEqual(Guid.Empty, id.Value);
    }

    [Fact]
    public void ConstructorRejectsEmptyGuid()
    {
        Assert.Throws<DomainValidationException>(() => new InventoryMovementId(Guid.Empty));
    }

    [Fact]
    public void IdsWithSameGuidAreEqual()
    {
        var guid = Guid.NewGuid();

        var first = new InventoryMovementId(guid);
        var second = new InventoryMovementId(guid);

        Assert.Equal(first, second);
    }

    [Fact]
    public void IdsWithDifferentGuidAreNotEqual()
    {
        var first = new InventoryMovementId(Guid.NewGuid());
        var second = new InventoryMovementId(Guid.NewGuid());

        Assert.NotEqual(first, second);
    }
}
