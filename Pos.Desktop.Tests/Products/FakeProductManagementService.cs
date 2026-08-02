using Pos.Application.Products.ManageProduct;
using Pos.Application.SalesCart;
using Pos.Domain.Common.Identifiers;

namespace Pos.Desktop.Tests.Products;

internal sealed class FakeProductManagementService : IProductManagementService
{
    private readonly Func<ProductId, CancellationToken, Task<ProductDetails?>>? _getByIdHandler;
    private readonly Func<UpdateProductRequest, CancellationToken, Task<UpdateProductResult>>? _updateHandler;
    private readonly Func<ProductId, bool, CancellationToken, Task<UpdateProductResult>>? _setActiveHandler;
    private readonly Func<AdjustProductInventoryRequest, CancellationToken, Task<AdjustProductInventoryResult>>? _adjustInventoryHandler;

    public FakeProductManagementService(
        Func<ProductId, CancellationToken, Task<ProductDetails?>>? getByIdHandler = null,
        Func<UpdateProductRequest, CancellationToken, Task<UpdateProductResult>>? updateHandler = null,
        Func<ProductId, bool, CancellationToken, Task<UpdateProductResult>>? setActiveHandler = null,
        Func<AdjustProductInventoryRequest, CancellationToken, Task<AdjustProductInventoryResult>>? adjustInventoryHandler = null)
    {
        _getByIdHandler = getByIdHandler;
        _updateHandler = updateHandler;
        _setActiveHandler = setActiveHandler;
        _adjustInventoryHandler = adjustInventoryHandler;
    }

    public int GetByIdCallCount { get; private set; }

    public int UpdateCallCount { get; private set; }

    public int SetActiveCallCount { get; private set; }

    public int AdjustInventoryCallCount { get; private set; }

    public UpdateProductRequest? LastUpdateRequest { get; private set; }

    public AdjustProductInventoryRequest? LastAdjustInventoryRequest { get; private set; }

    public bool? LastSetActiveValue { get; private set; }

    public Task<IReadOnlyList<ProductSearchResult>> SearchAsync(
        string searchTerm, bool includeInactive, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("No se usa en las pruebas de EditProductViewModel/AdjustInventoryViewModel.");

    public Task<ProductCatalogPageResult> GetCatalogPageAsync(
        string? searchTerm, ProductCatalogStatusFilter filter, int skip, int take, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("No se usa en las pruebas de EditProductViewModel/AdjustInventoryViewModel.");

    public Task<ProductCatalogSummary> GetDashboardSummaryAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("No se usa en las pruebas de EditProductViewModel/AdjustInventoryViewModel.");

    public Task<ProductDetails?> GetByIdAsync(ProductId productId, CancellationToken cancellationToken = default)
    {
        GetByIdCallCount++;

        return _getByIdHandler is null
            ? Task.FromResult<ProductDetails?>(null)
            : _getByIdHandler(productId, cancellationToken);
    }

    public Task<UpdateProductResult> UpdateAsync(
        UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        UpdateCallCount++;
        LastUpdateRequest = request;

        return _updateHandler is null
            ? Task.FromResult(UpdateProductResult.Failure(UpdateProductResultStatus.ProductNotFound))
            : _updateHandler(request, cancellationToken);
    }

    public Task<UpdateProductResult> SetActiveAsync(
        ProductId productId, bool isActive, CancellationToken cancellationToken = default)
    {
        SetActiveCallCount++;
        LastSetActiveValue = isActive;

        return _setActiveHandler is null
            ? Task.FromResult(UpdateProductResult.Failure(UpdateProductResultStatus.ProductNotFound))
            : _setActiveHandler(productId, isActive, cancellationToken);
    }

    public Task<AdjustProductInventoryResult> AdjustInventoryAsync(
        AdjustProductInventoryRequest request, CancellationToken cancellationToken = default)
    {
        AdjustInventoryCallCount++;
        LastAdjustInventoryRequest = request;

        return _adjustInventoryHandler is null
            ? Task.FromResult(AdjustProductInventoryResult.Failure(AdjustProductInventoryResultStatus.ProductNotFound))
            : _adjustInventoryHandler(request, cancellationToken);
    }
}
