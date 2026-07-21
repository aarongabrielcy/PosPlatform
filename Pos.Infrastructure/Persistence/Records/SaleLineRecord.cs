namespace Pos.Infrastructure.Persistence.Records;

internal sealed class SaleLineRecord
{
    public Guid Id { get; set; }

    public Guid SaleId { get; set; }

    public Guid ProductId { get; set; }

    public string ProductSku { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public decimal UnitPriceAmount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public SaleRecord Sale { get; set; } = null!;
}
