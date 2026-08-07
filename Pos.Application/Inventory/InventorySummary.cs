namespace Pos.Application.Inventory;

// Conteos agregados del scope actual (Organization+Branch), no de la página visible (TAREA 24G,
// sección 12/13): 4 consultas COUNT de costo fijo, igual criterio que ProductCatalogSummary.
public sealed record InventorySummary(
    int TrackedProductsCount,
    int InStockCount,
    int LowStockCount,
    int OutOfStockCount)
{
    public static InventorySummary Empty { get; } = new(0, 0, 0, 0);
}
