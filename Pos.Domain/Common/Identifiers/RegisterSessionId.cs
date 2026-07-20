using Pos.Domain.Common.Exceptions;

namespace Pos.Domain.Common.Identifiers;

public readonly record struct RegisterSessionId
{
    public Guid Value { get; }

    public RegisterSessionId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainValidationException("RegisterSessionId no puede ser Guid.Empty.");
        }

        Value = value;
    }

    public static RegisterSessionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
