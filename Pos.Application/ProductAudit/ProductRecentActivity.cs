using Pos.Domain.Common.Identifiers;
using Pos.Domain.ProductAudit;

namespace Pos.Application.ProductAudit;

// Resumen corto de la actividad reciente de un producto (TAREA 24D, sección 29/31): usado por el
// indicador de actividad reciente en Productos, nunca por Auditoría (que consulta ProductAuditEntry
// paginado completo). TopChanges trae como máximo 2 cambios para el resumen; TotalChangesCount
// permite mostrar "+ N cambios" sin cargar todos los ProductAuditFieldChange del evento.
public sealed class ProductRecentActivity
{
    public ProductId ProductId { get; }

    public ProductAuditEventId AuditEventId { get; }

    public ProductAuditAction Action { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    public string ActorDisplayName { get; }

    public IReadOnlyList<ProductAuditFieldChange> TopChanges { get; }

    public int TotalChangesCount { get; }

    public ProductRecentActivity(
        ProductId productId,
        ProductAuditEventId auditEventId,
        ProductAuditAction action,
        DateTimeOffset occurredAtUtc,
        string actorDisplayName,
        IReadOnlyList<ProductAuditFieldChange> topChanges,
        int totalChangesCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorDisplayName);
        ArgumentNullException.ThrowIfNull(topChanges);

        ProductId = productId;
        AuditEventId = auditEventId;
        Action = action;
        OccurredAtUtc = occurredAtUtc;
        ActorDisplayName = actorDisplayName;
        TopChanges = topChanges;
        TotalChangesCount = totalChangesCount;
    }
}
