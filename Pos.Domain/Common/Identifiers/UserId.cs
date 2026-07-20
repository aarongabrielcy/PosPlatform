using Pos.Domain.Common.Exceptions;

namespace Pos.Domain.Common.Identifiers;

public readonly record struct UserId
{
    public Guid Value { get; }

    public UserId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainValidationException("UserId no puede ser Guid.Empty.");
        }

        Value = value;
    }

    public static UserId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
