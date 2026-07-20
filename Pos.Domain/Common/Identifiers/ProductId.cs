using Pos.Domain.Common.Exceptions;

namespace Pos.Domain.Common.Identifiers;

public readonly record struct ProductId
{
    public Guid Value { get; }

    public ProductId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainValidationException("ProductId no puede ser Guid.Empty.");
        }

        Value = value;
    }

    public static ProductId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
