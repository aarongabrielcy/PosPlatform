using Pos.Domain.Common.Exceptions;

namespace Pos.Domain.Common.Identifiers;

public readonly record struct PaymentId
{
    public Guid Value { get; }

    public PaymentId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainValidationException("PaymentId no puede ser Guid.Empty.");
        }

        Value = value;
    }

    public static PaymentId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
