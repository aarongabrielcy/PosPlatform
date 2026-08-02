using Pos.Application.SalesCart;
using Pos.Domain.Common.Identifiers;

namespace Pos.Application.Products.ManageProduct;

public interface IProductManagementService
{
    // Búsqueda administrativa: igual criterio que ISalesCartService.SearchProductsAsync, pero
    // opcionalmente incluye productos inactivos. No debe usarse para poblar el carrito de venta.
    Task<IReadOnlyList<ProductSearchResult>> SearchAsync(
        string searchTerm, bool includeInactive, CancellationToken cancellationToken = default);

    Task<ProductDetails?> GetByIdAsync(ProductId productId, CancellationToken cancellationToken = default);

    // Catálogo administrativo paginado para ProductsView (sección 8-13 de TAREA 24C). Requiere
    // ManageProducts y una caja actual (de la que se resuelve el Branch para la existencia), igual
    // que SearchAsync. take es el tamaño de página solicitado; la paginación "hay página
    // siguiente" se resuelve internamente pidiendo una fila de más (ver ProductCatalogPageResult).
    Task<ProductCatalogPageResult> GetCatalogPageAsync(
        string? searchTerm,
        ProductCatalogStatusFilter filter,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    // Conteos para el Dashboard (TAREA 24C.1): mismo permiso (ManageProducts) y resolución de
    // Branch que GetCatalogPageAsync.
    Task<ProductCatalogSummary> GetDashboardSummaryAsync(CancellationToken cancellationToken = default);

    Task<UpdateProductResult> UpdateAsync(
        UpdateProductRequest request, CancellationToken cancellationToken = default);

    Task<UpdateProductResult> SetActiveAsync(
        ProductId productId, bool isActive, CancellationToken cancellationToken = default);

    Task<AdjustProductInventoryResult> AdjustInventoryAsync(
        AdjustProductInventoryRequest request, CancellationToken cancellationToken = default);
}
