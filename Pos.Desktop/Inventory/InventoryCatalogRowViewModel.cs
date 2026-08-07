using Pos.Application.Inventory;

namespace Pos.Desktop.Inventory;

// Fila de la tabla "Existencias" (TAREA 24G, sección 15): envuelve InventoryCatalogItem con texto
// en español ya formateado para XAML, igual criterio que ProductAuditRowViewModel. El texto de
// estado nunca depende únicamente de un color (sección 15 de la tarea).
public sealed class InventoryCatalogRowViewModel
{
    public InventoryCatalogItem Item { get; }

    public string StockStatusText { get; }

    public string IsActiveText => Item.IsActive ? "Activo" : "Inactivo";

    public InventoryCatalogRowViewModel(InventoryCatalogItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        Item = item;
        StockStatusText = item.StockStatus switch
        {
            InventoryStockStatus.OutOfStock => "Sin existencia",
            InventoryStockStatus.LowStock => "Stock bajo",
            InventoryStockStatus.InStock => "Con stock",
            _ => string.Empty,
        };
    }
}
