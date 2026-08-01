using Pos.Domain.Common.Identifiers;

namespace Pos.Application.SalesCart;

public interface ISalesCartService
{
    Task<IReadOnlyList<ProductSearchResult>> SearchProductsAsync(
        string searchTerm, CancellationToken cancellationToken = default);

    Task<SalesCartResult> AddProductAsync(
        AddProductToCartRequest request, CancellationToken cancellationToken = default);

    Task<SalesCartResult> UpdateQuantityAsync(
        UpdateCartLineQuantityRequest request, CancellationToken cancellationToken = default);

    SalesCartResult RemoveLine(ProductId productId);

    SalesCartResult Clear();
}
