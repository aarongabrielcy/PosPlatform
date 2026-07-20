using Pos.Domain.Common.Exceptions;

namespace Pos.Domain.Common.Identifiers;

public readonly record struct SaleLineId
{
    public Guid Value { get; }

    public SaleLineId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainValidationException("SaleLineId no puede ser Guid.Empty.");
        }

        Value = value;
    }

    public static SaleLineId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
