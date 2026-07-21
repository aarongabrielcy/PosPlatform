using Pos.Domain.Common.Exceptions;

namespace Pos.Domain.Common.Identifiers;

public readonly record struct InventoryMovementId
{
    public Guid Value { get; }

    public InventoryMovementId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainValidationException("InventoryMovementId no puede ser Guid.Empty.");
        }

        Value = value;
    }

    public static InventoryMovementId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
