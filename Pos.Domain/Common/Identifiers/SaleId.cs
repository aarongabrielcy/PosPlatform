using Pos.Domain.Common.Exceptions;

namespace Pos.Domain.Common.Identifiers;

public readonly record struct SaleId
{
    public Guid Value { get; }

    public SaleId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainValidationException("SaleId no puede ser Guid.Empty.");
        }

        Value = value;
    }

    public static SaleId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
