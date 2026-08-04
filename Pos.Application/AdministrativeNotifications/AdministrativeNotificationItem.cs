using Pos.Application.ProductAudit;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.ProductAudit;

namespace Pos.Application.AdministrativeNotifications;

// Proyección de solo lectura para el centro de notificaciones (TAREA 24E, sección 20): no
// duplica el contenido de ProductAudit más allá de lo necesario para pintar la lista sin volver a
// consultar Auditoría; Changes reutiliza ProductAuditFieldChange, el mismo DTO que ya usa
// Auditoría > Productos.
public sealed class AdministrativeNotificationItem
{
    public AdministrativeNotificationId NotificationId { get; }

    public ProductAuditEventId AuditEventId { get; }

    public ProductId ProductId { get; }

    public string ProductSku { get; }

    public string ProductName { get; }

    public string ActorDisplayName { get; }

    public ProductAuditAction AuditAction { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    public DateTimeOffset? ReadAtUtc { get; }

    public bool IsRead => ReadAtUtc is not null;

    public IReadOnlyList<ProductAuditFieldChange> Changes { get; }

    public AdministrativeNotificationItem(
        AdministrativeNotificationId notificationId,
        ProductAuditEventId auditEventId,
        ProductId productId,
        string productSku,
        string productName,
        string actorDisplayName,
        ProductAuditAction auditAction,
        DateTimeOffset occurredAtUtc,
        DateTimeOffset? readAtUtc,
        IReadOnlyList<ProductAuditFieldChange> changes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productSku);
        ArgumentException.ThrowIfNullOrWhiteSpace(productName);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorDisplayName);
        ArgumentNullException.ThrowIfNull(changes);

        NotificationId = notificationId;
        AuditEventId = auditEventId;
        ProductId = productId;
        ProductSku = productSku;
        ProductName = productName;
        ActorDisplayName = actorDisplayName;
        AuditAction = auditAction;
        OccurredAtUtc = occurredAtUtc;
        ReadAtUtc = readAtUtc;
        Changes = changes;
    }
}
