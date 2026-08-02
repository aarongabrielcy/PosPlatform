using Pos.Application.Products.ManageProduct;
using Pos.Application.SalesCart;
using Pos.Domain.Common.Identifiers;

namespace Pos.Desktop.Tests.Products.Catalog;

internal sealed class FakeProductManagementService : IProductManagementService
{
    private readonly Func<string?, ProductCatalogStatusFilter, int, int, CancellationToken, Task<ProductCatalogPageResult>>? _catalogHandler;
    private readonly ProductCatalogSummary _summary;

    public FakeProductManagementService(
        Func<string?, ProductCatalogStatusFilter, int, int, CancellationToken, Task<ProductCatalogPageResult>>? catalogHandler = null,
        ProductCatalogSummary? summary = null)
    {
        _catalogHandler = catalogHandler;
        _summary = summary ?? ProductCatalogSummary.Empty;
    }

    public int GetCatalogPageCallCount { get; private set; }

    public int GetDashboardSummaryCallCount { get; private set; }

    public string? LastSearchTerm { get; private set; }

    public ProductCatalogStatusFilter? LastFilter { get; private set; }

    public int? LastSkip { get; private set; }

    public int? LastTake { get; private set; }

    public Task<IReadOnlyList<ProductSearchResult>> SearchAsync(
        string searchTerm, bool includeInactive, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("No se usa en las pruebas de ProductsViewModel.");

    public Task<ProductDetails?> GetByIdAsync(ProductId productId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("No se usa en las pruebas de ProductsViewModel.");

    public Task<UpdateProductResult> UpdateAsync(
        UpdateProductRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("No se usa en las pruebas de ProductsViewModel.");

    public Task<UpdateProductResult> SetActiveAsync(
        ProductId productId, bool isActive, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("No se usa en las pruebas de ProductsViewModel.");

    public Task<AdjustProductInventoryResult> AdjustInventoryAsync(
        AdjustProductInventoryRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("No se usa en las pruebas de ProductsViewModel.");

    public Task<ProductCatalogPageResult> GetCatalogPageAsync(
        string? searchTerm, ProductCatalogStatusFilter filter, int skip, int take, CancellationToken cancellationToken = default)
    {
        GetCatalogPageCallCount++;
        LastSearchTerm = searchTerm;
        LastFilter = filter;
        LastSkip = skip;
        LastTake = take;

        return _catalogHandler is null
            ? Task.FromResult(ProductCatalogPageResult.Empty)
            : _catalogHandler(searchTerm, filter, skip, take, cancellationToken);
    }

    public Task<ProductCatalogSummary> GetDashboardSummaryAsync(CancellationToken cancellationToken = default)
    {
        GetDashboardSummaryCallCount++;

        return Task.FromResult(_summary);
    }
}
