using Pos.Domain.Common.Identifiers;

namespace Pos.Application.Products.ManageProduct;

// Proyección de Product+InventoryItem para la pantalla de edición. No expone la entidad Domain
// ni ProductRecord/InventoryItemRecord, ni tipos de Domain como Money/Sku/Barcode: solo
// primitivos, igual que ProductSearchResult, para que Pos.Desktop nunca necesite depender de
// Pos.Domain.
public sealed class ProductDetails
{
    public ProductId ProductId { get; }

    public string Sku { get; }

    public string? Barcode { get; }

    public string Name { get; }

    public string? Description { get; }

    public decimal SalePriceAmount { get; }

    public string Currency { get; }

    public decimal? CostAmount { get; }

    public bool TracksInventory { get; }

    public bool IsActive { get; }

    // Solo tiene significado cuando TracksInventory es true. Para productos sin control de
    // inventario permanece en cero: no se inventa una existencia infinita.
    public decimal CurrentQuantity { get; }

    public decimal ReorderPoint { get; }

    public ProductDetails(
        ProductId productId,
        string sku,
        string? barcode,
        string name,
        string? description,
        decimal salePriceAmount,
        string currency,
        decimal? costAmount,
        bool tracksInventory,
        bool isActive,
        decimal currentQuantity,
        decimal reorderPoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        ProductId = productId;
        Sku = sku;
        Barcode = barcode;
        Name = name;
        Description = description;
        SalePriceAmount = salePriceAmount;
        Currency = currency;
        CostAmount = costAmount;
        TracksInventory = tracksInventory;
        IsActive = isActive;
        CurrentQuantity = currentQuantity;
        ReorderPoint = reorderPoint;
    }
}
