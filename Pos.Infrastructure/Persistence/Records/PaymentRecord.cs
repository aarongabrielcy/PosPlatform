using Pos.Domain.Sales;

namespace Pos.Infrastructure.Persistence.Records;

internal sealed class PaymentRecord
{
    public Guid Id { get; set; }

    public Guid SaleId { get; set; }

    public PaymentMethod Method { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public DateTimeOffset PaidAtUtc { get; set; }

    public string? Reference { get; set; }

    public SaleRecord Sale { get; set; } = null!;
}
