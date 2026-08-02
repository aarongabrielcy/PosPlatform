using Pos.Application.Products.ManageProduct;
using Pos.Domain.Common.Identifiers;

namespace Pos.Application.Products;

// Consulta especializada de solo lectura para el catálogo administrativo (ProductsView): combina
// Product+InventoryItem (por Branch) en una sola consulta con filtro/orden/paginación aplicados en
// el proveedor de datos, evitando el N+1 de resolver la existencia producto por producto en
// Application (ver TAREA 24C, sección 10). Vive en Application porque expone solo tipos
// Application (DTOs), pero su implementación cruza Records de Infrastructure y por eso no puede
// vivir en IProductRepository (que solo devuelve entidades Product de Domain).
public interface IProductCatalogQuery
{
    Task<ProductCatalogPageResult> SearchPageAsync(
        OrganizationId organizationId,
        BranchId branchId,
        string? searchTerm,
        ProductCatalogStatusFilter filter,
        int skip,
        int take,
        CancellationToken cancellationToken);

    // Conteos para el Dashboard (TAREA 24C.1): mismo criterio de Activo/Stock bajo/Sin existencia
    // que SearchPageAsync, pero como agregados en vez de páginas de filas.
    Task<ProductCatalogSummary> GetSummaryAsync(
        OrganizationId organizationId,
        BranchId branchId,
        CancellationToken cancellationToken);
}
