using Pos.Domain.Common.Identifiers;
using Pos.Domain.ProductAudit;

namespace Pos.Application.ProductAudit;

// Proyección de solo lectura de ProductAuditEvent para el módulo administrativo Auditoría >
// Productos. No expone la entidad Domain ni Records de Infrastructure.
public sealed class ProductAuditEntry
{
    public ProductAuditEventId AuditEventId { get; }

    public ProductId ProductId { get; }

    public string ProductSku { get; }

    public string ProductName { get; }

    public UserId ActorUserId { get; }

    public string ActorUsername { get; }

    public string ActorDisplayName { get; }

    public ProductAuditAction Action { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    public IReadOnlyList<ProductAuditFieldChange> Changes { get; }

    public ProductAuditEntry(
        ProductAuditEventId auditEventId,
        ProductId productId,
        string productSku,
        string productName,
        UserId actorUserId,
        string actorUsername,
        string actorDisplayName,
        ProductAuditAction action,
        DateTimeOffset occurredAtUtc,
        IReadOnlyList<ProductAuditFieldChange> changes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productSku);
        ArgumentException.ThrowIfNullOrWhiteSpace(productName);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorUsername);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorDisplayName);
        ArgumentNullException.ThrowIfNull(changes);

        AuditEventId = auditEventId;
        ProductId = productId;
        ProductSku = productSku;
        ProductName = productName;
        ActorUserId = actorUserId;
        ActorUsername = actorUsername;
        ActorDisplayName = actorDisplayName;
        Action = action;
        OccurredAtUtc = occurredAtUtc;
        Changes = changes;
    }
}
