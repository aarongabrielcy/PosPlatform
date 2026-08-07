namespace Pos.Application.Inventory;

// Estado derivado, nunca persistido (TAREA 24G, sección 7): se calcula en la consulta a partir de
// Quantity/ReorderPoint, igual criterio que ProductCatalogStatusFilter.LowStock/OutOfStock.
public enum InventoryStockStatus
{
    InStock,
    LowStock,
    OutOfStock,
}
