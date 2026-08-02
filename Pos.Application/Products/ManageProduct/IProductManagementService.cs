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

    Task<UpdateProductResult> UpdateAsync(
        UpdateProductRequest request, CancellationToken cancellationToken = default);

    Task<UpdateProductResult> SetActiveAsync(
        ProductId productId, bool isActive, CancellationToken cancellationToken = default);

    Task<AdjustProductInventoryResult> AdjustInventoryAsync(
        AdjustProductInventoryRequest request, CancellationToken cancellationToken = default);
}
