using Pos.Domain.Common.Exceptions;

namespace Pos.Domain.Common.Identifiers;

public readonly record struct CashMovementId
{
    public Guid Value { get; }

    public CashMovementId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainValidationException("CashMovementId no puede ser Guid.Empty.");
        }

        Value = value;
    }

    public static CashMovementId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
