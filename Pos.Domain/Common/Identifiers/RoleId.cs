using Pos.Domain.Common.Exceptions;

namespace Pos.Domain.Common.Identifiers;

public readonly record struct RoleId
{
    public Guid Value { get; }

    public RoleId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainValidationException("RoleId no puede ser Guid.Empty.");
        }

        Value = value;
    }

    public static RoleId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
