using Pos.Application.AdministrativeNotifications;
using Pos.Desktop.Common;

namespace Pos.Desktop.AdministrativeNotifications;

// Fila del centro de notificaciones (TAREA 24E, sección 26): envuelve AdministrativeNotificationItem
// con texto ya formateado para XAML, igual criterio que ProductAuditRowViewModel con ProductAuditEntry.
public sealed class AdministrativeNotificationRowViewModel
{
    public AdministrativeNotificationItem Item { get; }

    public string Title { get; }

    public string ProductSku => Item.ProductSku;

    public string ProductName => Item.ProductName;

    public string ActorDisplayName => Item.ActorDisplayName;

    public string SummaryText { get; }

    public string RelativeTimeText { get; }

    public bool IsRead => Item.IsRead;

    // "Nueva" únicamente si no está leída (TAREA 24E, sección 35): no depende solo del color.
    public string StatusLabel => IsRead ? string.Empty : "Nueva";

    public AdministrativeNotificationRowViewModel(AdministrativeNotificationItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        Item = item;
        Title = AdministrativeNotificationDisplayFormatter.ToTitle(item.AuditAction, item.Changes);
        SummaryText = AdministrativeNotificationDisplayFormatter.ToSummary(item.Changes);
        RelativeTimeText = ProductAuditDisplayFormatter.ToRelativeTime(item.OccurredAtUtc, DateTimeOffset.UtcNow);
    }
}
