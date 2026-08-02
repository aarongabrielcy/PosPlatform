namespace Pos.Application.Products.ManageProduct;

// HasNextPage se resuelve pidiendo "take + 1" filas a la consulta y recortando la última: evita
// un COUNT(*) adicional sobre catálogos grandes (ver TAREA 24C, sección 9).
public sealed record ProductCatalogPageResult(IReadOnlyList<ProductCatalogItem> Items, bool HasNextPage)
{
    public static ProductCatalogPageResult Empty { get; } = new(Array.Empty<ProductCatalogItem>(), false);
}
