using Pos.Domain.AdministrativeNotifications;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Persistence.Mappers;

// Solo ToRecord (TAREA 24E): append-only, igual patrón que ProductAuditEventMapper. MarkRead
// nunca rehidrata AdministrativeNotification como entidad Domain; opera directamente sobre el
// AdministrativeNotificationRecipientRecord ya trackeado por el ChangeTracker.
internal static class AdministrativeNotificationMapper
{
    internal static AdministrativeNotificationRecord ToRecord(AdministrativeNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var record = new AdministrativeNotificationRecord
        {
            Id = notification.Id.Value,
            OrganizationId = notification.OrganizationId.Value,
            ProductAuditEventId = notification.ProductAuditEventId.Value,
            CreatedAtUtc = notification.CreatedAtUtc,
        };

        record.Recipients = notification.Recipients
            .Select(recipient => new AdministrativeNotificationRecipientRecord
            {
                NotificationId = record.Id,
                UserId = recipient.UserId.Value,
                ReadAtUtc = recipient.ReadAtUtc,
            })
            .ToList();

        return record;
    }
}
