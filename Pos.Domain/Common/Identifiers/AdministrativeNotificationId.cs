using Pos.Domain.Common.Exceptions;

namespace Pos.Domain.Common.Identifiers;

public readonly record struct AdministrativeNotificationId
{
    public Guid Value { get; }

    public AdministrativeNotificationId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new DomainValidationException("AdministrativeNotificationId no puede ser Guid.Empty.");
        }

        Value = value;
    }

    public static AdministrativeNotificationId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
