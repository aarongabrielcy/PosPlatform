namespace Pos.Application.Sales.History;

// HasNextPage se resuelve pidiendo "take + 1" filas y recortando la última, mismo patrón que
// ProductAuditPageResult/InventoryMovementPageResult (TAREA 25B, sección 14): evita un COUNT(*)
// adicional en el listado.
public sealed record SalesHistoryPageResult(IReadOnlyList<SalesHistoryItem> Items, bool HasNextPage)
{
    public static SalesHistoryPageResult Empty { get; } = new(Array.Empty<SalesHistoryItem>(), false);
}
