using Pos.Domain.Common.Identifiers;

namespace Pos.Application.Products.ManageProduct;

// Proyección de Product+InventoryItem para una fila del catálogo (ProductsView). No expone la
// entidad Domain ni Records de Infrastructure, ni tipos Domain como Money/Sku/Barcode: solo
// primitivos, igual que ProductSearchResult/ProductDetails.
public sealed class ProductCatalogItem
{
    public ProductId ProductId { get; }

    public string Sku { get; }

    public string? Barcode { get; }

    public string Name { get; }

    public decimal SalePriceAmount { get; }

    public string Currency { get; }

    public bool TracksInventory { get; }

    // Solo tiene significado cuando TracksInventory es true.
    public decimal Quantity { get; }

    public decimal ReorderPoint { get; }

    public bool IsActive { get; }

    public ProductCatalogItem(
        ProductId productId,
        string sku,
        string? barcode,
        string name,
        decimal salePriceAmount,
        string currency,
        bool tracksInventory,
        decimal quantity,
        decimal reorderPoint,
        bool isActive)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        ProductId = productId;
        Sku = sku;
        Barcode = barcode;
        Name = name;
        SalePriceAmount = salePriceAmount;
        Currency = currency;
        TracksInventory = tracksInventory;
        Quantity = quantity;
        ReorderPoint = reorderPoint;
        IsActive = isActive;
    }
}
