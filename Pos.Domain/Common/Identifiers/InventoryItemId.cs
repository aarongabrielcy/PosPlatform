using Pos.Domain.Common.Exceptions;

namespace Pos.Domain.Common.Identifiers;

public readonly record struct InventoryItemId
{
    public Guid Value { get; }

    public InventoryItemId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainValidationException("InventoryItemId no puede ser Guid.Empty.");
        }

        Value = value;
    }

    public static InventoryItemId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
