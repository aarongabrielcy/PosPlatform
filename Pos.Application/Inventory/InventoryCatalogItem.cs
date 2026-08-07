using Pos.Domain.Common.Identifiers;

namespace Pos.Application.Inventory;

// Proyección de Product+InventoryItem para una fila del módulo operativo de Inventario (TAREA
// 24G): solo productos con TracksInventory == true. No expone entidades Domain ni Records de
// Infrastructure, igual criterio que ProductCatalogItem.
public sealed class InventoryCatalogItem
{
    public ProductId ProductId { get; }

    public string Sku { get; }

    public string? Barcode { get; }

    public string ProductName { get; }

    public bool IsActive { get; }

    public decimal Quantity { get; }

    public decimal ReorderPoint { get; }

    public InventoryStockStatus StockStatus { get; }

    public InventoryCatalogItem(
        ProductId productId,
        string sku,
        string? barcode,
        string productName,
        bool isActive,
        decimal quantity,
        decimal reorderPoint,
        InventoryStockStatus stockStatus)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentException.ThrowIfNullOrWhiteSpace(productName);

        ProductId = productId;
        Sku = sku;
        Barcode = barcode;
        ProductName = productName;
        IsActive = isActive;
        Quantity = quantity;
        ReorderPoint = reorderPoint;
        StockStatus = stockStatus;
    }
}
