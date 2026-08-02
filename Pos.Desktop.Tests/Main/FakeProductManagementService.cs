using Pos.Application.Products.ManageProduct;
using Pos.Application.SalesCart;
using Pos.Domain.Common.Identifiers;

namespace Pos.Desktop.Tests.Main;

internal sealed class FakeProductManagementService : IProductManagementService
{
    private readonly Func<string, bool, CancellationToken, Task<IReadOnlyList<ProductSearchResult>>>? _searchHandler;

    public FakeProductManagementService(
        Func<string, bool, CancellationToken, Task<IReadOnlyList<ProductSearchResult>>>? searchHandler = null)
    {
        _searchHandler = searchHandler;
    }

    public int SearchCallCount { get; private set; }

    public string? LastSearchTerm { get; private set; }

    public bool? LastIncludeInactive { get; private set; }

    public Task<IReadOnlyList<ProductSearchResult>> SearchAsync(
        string searchTerm, bool includeInactive, CancellationToken cancellationToken = default)
    {
        SearchCallCount++;
        LastSearchTerm = searchTerm;
        LastIncludeInactive = includeInactive;

        return _searchHandler is null
            ? Task.FromResult<IReadOnlyList<ProductSearchResult>>(Array.Empty<ProductSearchResult>())
            : _searchHandler(searchTerm, includeInactive, cancellationToken);
    }

    public Task<ProductDetails?> GetByIdAsync(ProductId productId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("No se usa en las pruebas de MainWindowViewModel.");

    public Task<UpdateProductResult> UpdateAsync(
        UpdateProductRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("No se usa en las pruebas de MainWindowViewModel.");

    public Task<UpdateProductResult> SetActiveAsync(
        ProductId productId, bool isActive, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("No se usa en las pruebas de MainWindowViewModel.");

    public Task<AdjustProductInventoryResult> AdjustInventoryAsync(
        AdjustProductInventoryRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("No se usa en las pruebas de MainWindowViewModel.");
}
