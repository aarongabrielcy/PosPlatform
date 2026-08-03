namespace Pos.Application.ProductAudit;

// HasNextPage se resuelve pidiendo "take + 1" filas y recortando la última, igual que
// ProductCatalogPageResult (ver TAREA 24C, sección 9 / TAREA 24D, sección 20): evita un COUNT(*)
// adicional.
public sealed record ProductAuditPageResult(IReadOnlyList<ProductAuditEntry> Items, bool HasNextPage)
{
    public static ProductAuditPageResult Empty { get; } = new(Array.Empty<ProductAuditEntry>(), false);
}
