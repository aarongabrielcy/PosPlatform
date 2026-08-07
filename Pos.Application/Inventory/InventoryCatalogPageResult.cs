namespace Pos.Application.Inventory;

// HasNextPage se resuelve pidiendo "take + 1" filas y recortando la última, igual criterio que
// ProductCatalogPageResult/ProductAuditPageResult: evita un COUNT(*) adicional.
public sealed record InventoryCatalogPageResult(IReadOnlyList<InventoryCatalogItem> Items, bool HasNextPage)
{
    public static InventoryCatalogPageResult Empty { get; } = new(Array.Empty<InventoryCatalogItem>(), false);
}
