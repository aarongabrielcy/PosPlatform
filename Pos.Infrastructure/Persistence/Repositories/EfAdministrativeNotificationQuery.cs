using Microsoft.EntityFrameworkCore;
using Pos.Application.AdministrativeNotifications;
using Pos.Application.ProductAudit;
using Pos.Domain.Common.Identifiers;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Persistence.Repositories;

// GetForUserAsync resuelve la página (recipient+notification) y despues trae los ProductAuditEvent
// referenciados en un solo lote por Id (TAREA 24E, sección 20): dos consultas en total,
// independientes del tamaño de la página, igual criterio "sin N+1" que
// EfProductAuditQuery.GetRecentActivityAsync.
public sealed class EfAdministrativeNotificationQuery : IAdministrativeNotificationQuery
{
    private readonly PosDbContext _context;

    public EfAdministrativeNotificationQuery(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<AdministrativeNotificationPageResult> GetForUserAsync(
        OrganizationId organizationId, UserId userId, int skip, int take, CancellationToken cancellationToken)
    {
        if (skip < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(skip), skip, "skip no puede ser negativo.");
        }

        if (take <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(take), take, "take debe ser mayor que cero.");
        }

        var query =
            from recipient in _context.AdministrativeNotificationRecipients.AsNoTracking()
            join notification in _context.AdministrativeNotifications.AsNoTracking()
                on recipient.NotificationId equals notification.Id
            where recipient.UserId == userId.Value && notification.OrganizationId == organizationId.Value
            orderby notification.CreatedAtUtc descending, notification.Id descending
            select new { recipient, notification };

        var page = await query.Skip(skip).Take(take + 1).ToListAsync(cancellationToken);

        var hasNextPage = page.Count > take;

        if (hasNextPage)
        {
            page.RemoveAt(page.Count - 1);
        }

        if (page.Count == 0)
        {
            return new AdministrativeNotificationPageResult(Array.Empty<AdministrativeNotificationItem>(), false);
        }

        var auditEventIds = page.Select(p => p.notification.ProductAuditEventId).Distinct().ToList();

        var auditEventsById = await _context.ProductAuditEvents.AsNoTracking()
            .Include(e => e.Changes)
            .Where(e => auditEventIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, cancellationToken);

        var items = new List<AdministrativeNotificationItem>(page.Count);

        foreach (var row in page)
        {
            if (!auditEventsById.TryGetValue(row.notification.ProductAuditEventId, out var auditEvent))
            {
                continue;
            }

            items.Add(ToItem(row.notification, row.recipient, auditEvent));
        }

        return new AdministrativeNotificationPageResult(items, hasNextPage);
    }

    public Task<int> GetUnreadCountAsync(OrganizationId organizationId, UserId userId, CancellationToken cancellationToken)
    {
        var query =
            from recipient in _context.AdministrativeNotificationRecipients.AsNoTracking()
            join notification in _context.AdministrativeNotifications.AsNoTracking()
                on recipient.NotificationId equals notification.Id
            where recipient.UserId == userId.Value
                && notification.OrganizationId == organizationId.Value
                && recipient.ReadAtUtc == null
            select recipient.NotificationId;

        return query.CountAsync(cancellationToken);
    }

    private static AdministrativeNotificationItem ToItem(
        AdministrativeNotificationRecord notification,
        AdministrativeNotificationRecipientRecord recipient,
        ProductAuditEventRecord auditEvent) =>
        new(
            new AdministrativeNotificationId(notification.Id),
            new ProductAuditEventId(auditEvent.Id),
            new ProductId(auditEvent.ProductId),
            auditEvent.ProductSkuSnapshot,
            auditEvent.ProductNameSnapshot,
            auditEvent.ActorDisplayNameSnapshot,
            auditEvent.Action,
            auditEvent.OccurredAtUtc,
            recipient.ReadAtUtc,
            auditEvent.Changes
                .Select(change => new ProductAuditFieldChange(change.FieldName, change.OldValue, change.NewValue))
                .ToList());
}
