using Pos.Domain.Common.Exceptions;

namespace Pos.Domain.Common.Identifiers;

public readonly record struct ProductAuditChangeId
{
    public Guid Value { get; }

    public ProductAuditChangeId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainValidationException("ProductAuditChangeId no puede ser Guid.Empty.");
        }

        Value = value;
    }

    public static ProductAuditChangeId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
