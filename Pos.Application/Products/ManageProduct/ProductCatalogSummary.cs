namespace Pos.Application.Products.ManageProduct;

// Conteos agregados del catálogo para el Dashboard (TAREA 24C.1): 3 consultas COUNT de costo fijo
// (no dependen del tamaño del catálogo), nunca N+1 por producto.
public sealed record ProductCatalogSummary(int TotalProducts, int LowStockCount, int OutOfStockCount)
{
    public static ProductCatalogSummary Empty { get; } = new(0, 0, 0);
}
