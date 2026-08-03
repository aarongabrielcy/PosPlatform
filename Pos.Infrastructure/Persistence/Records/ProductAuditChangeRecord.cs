using Pos.Domain.ProductAudit;

namespace Pos.Infrastructure.Persistence.Records;

internal sealed class ProductAuditChangeRecord
{
    public Guid Id { get; set; }

    public Guid ProductAuditEventId { get; set; }

    public ProductAuditField FieldName { get; set; }

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public ProductAuditEventRecord ProductAuditEvent { get; set; } = null!;
}
