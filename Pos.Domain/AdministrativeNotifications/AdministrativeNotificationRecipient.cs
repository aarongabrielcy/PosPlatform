using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;

namespace Pos.Domain.AdministrativeNotifications;

// Fila hija de AdministrativeNotification (TAREA 24E, sección 6/7): representa que un User
// determinado recibió la notificación. ReadAtUtc solo puede pasar de null a un timestamp UTC;
// nunca vuelve a null. Sin Id propio: su identidad natural es (NotificationId, UserId), igual
// patrón que RolePermissionRecord en Infrastructure.
public sealed class AdministrativeNotificationRecipient
{
    public AdministrativeNotificationId NotificationId { get; }

    public UserId UserId { get; }

    public DateTimeOffset? ReadAtUtc { get; private set; }

    public bool IsRead => ReadAtUtc is not null;

    internal AdministrativeNotificationRecipient(AdministrativeNotificationId notificationId, UserId userId)
    {
        NotificationId = EnsureNotEmpty(notificationId);
        UserId = EnsureNotEmpty(userId);
        ReadAtUtc = null;
    }

    // Idempotente (TAREA 24E, sección 22): marcar leído un receipt ya leído no cambia ReadAtUtc.
    public void MarkRead(DateTimeOffset nowUtc)
    {
        if (ReadAtUtc is not null)
        {
            return;
        }

        ReadAtUtc = EnsureUtc(nowUtc);
    }

    private static AdministrativeNotificationId EnsureNotEmpty(AdministrativeNotificationId notificationId)
    {
        if (notificationId.Value == Guid.Empty)
        {
            throw new DomainValidationException("NotificationId no puede ser vacío.");
        }

        return notificationId;
    }

    private static UserId EnsureNotEmpty(UserId userId)
    {
        if (userId.Value == Guid.Empty)
        {
            throw new DomainValidationException("UserId no puede ser vacío.");
        }

        return userId;
    }

    private static DateTimeOffset EnsureUtc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new DomainValidationException("ReadAtUtc debe tener Offset igual a TimeSpan.Zero.");
        }

        return value;
    }
}
