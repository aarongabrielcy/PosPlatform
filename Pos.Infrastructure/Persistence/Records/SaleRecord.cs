using Pos.Domain.Sales;

namespace Pos.Infrastructure.Persistence.Records;

internal sealed class SaleRecord
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public Guid RegisterSessionId { get; set; }

    public Guid CreatedByUserId { get; set; }

    public string Currency { get; set; } = string.Empty;

    public SaleStatus Status { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public List<SaleLineRecord> Lines { get; set; } = new();

    public List<PaymentRecord> Payments { get; set; } = new();
}
