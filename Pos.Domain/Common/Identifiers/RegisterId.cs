using Pos.Domain.Common.Exceptions;

namespace Pos.Domain.Common.Identifiers;

public readonly record struct RegisterId
{
    public Guid Value { get; }

    public RegisterId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainValidationException("RegisterId no puede ser Guid.Empty.");
        }

        Value = value;
    }

    public static RegisterId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
