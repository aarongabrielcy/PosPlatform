using Pos.Domain.ProductAudit;

namespace Pos.Infrastructure.Persistence.Records;

internal sealed class ProductAuditEventRecord
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid ProductId { get; set; }

    public Guid ActorUserId { get; set; }

    public string ActorUsernameSnapshot { get; set; } = string.Empty;

    public string ActorDisplayNameSnapshot { get; set; } = string.Empty;

    public string ProductSkuSnapshot { get; set; } = string.Empty;

    public string ProductNameSnapshot { get; set; } = string.Empty;

    public ProductAuditAction Action { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    public List<ProductAuditChangeRecord> Changes { get; set; } = [];
}
