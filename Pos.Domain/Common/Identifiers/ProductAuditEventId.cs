using Pos.Domain.Common.Exceptions;

namespace Pos.Domain.Common.Identifiers;

public readonly record struct ProductAuditEventId
{
    public Guid Value { get; }

    public ProductAuditEventId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainValidationException("ProductAuditEventId no puede ser Guid.Empty.");
        }

        Value = value;
    }

    public static ProductAuditEventId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
