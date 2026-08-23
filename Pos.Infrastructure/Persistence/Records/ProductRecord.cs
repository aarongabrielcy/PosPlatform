namespace Pos.Infrastructure.Persistence.Records;

internal sealed class ProductRecord
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string? Barcode { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal SalePriceAmount { get; set; }

    public string SalePriceCurrency { get; set; } = string.Empty;

    public decimal? CostAmount { get; set; }

    public string? CostCurrency { get; set; }

    public bool TracksInventory { get; set; }

    public bool IsActive { get; set; }

    public string? ImageFileName { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
