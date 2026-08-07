namespace Pos.Application.Inventory;

// HasNextPage se resuelve pidiendo "take + 1" filas y recortando la última, igual criterio que
// InventoryCatalogPageResult/ProductAuditPageResult: evita un COUNT(*) adicional.
public sealed record InventoryMovementPageResult(IReadOnlyList<InventoryMovementItem> Items, bool HasNextPage)
{
    public static InventoryMovementPageResult Empty { get; } = new(Array.Empty<InventoryMovementItem>(), false);
}
