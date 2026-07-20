using Pos.Domain.Common.Exceptions;

namespace Pos.Domain.Common.Identifiers;

public readonly record struct OrganizationId
{
    public Guid Value { get; }

    public OrganizationId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainValidationException("OrganizationId no puede ser Guid.Empty.");
        }

        Value = value;
    }

    public static OrganizationId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
