using Pos.Application.SalesCart;
using Pos.Domain.Common.Identifiers;

namespace Pos.Desktop.Tests.Main;

internal sealed class FakeSalesCartService : ISalesCartService
{
    private readonly Func<string, CancellationToken, Task<IReadOnlyList<ProductSearchResult>>>? _searchHandler;
    private readonly Func<AddProductToCartRequest, CancellationToken, Task<SalesCartResult>>? _addHandler;
    private readonly Func<UpdateCartLineQuantityRequest, CancellationToken, Task<SalesCartResult>>? _updateHandler;
    private readonly Func<ProductId, SalesCartResult>? _removeHandler;
    private readonly Func<SalesCartResult>? _clearHandler;

    public FakeSalesCartService(
        Func<string, CancellationToken, Task<IReadOnlyList<ProductSearchResult>>>? searchHandler = null,
        Func<AddProductToCartRequest, CancellationToken, Task<SalesCartResult>>? addHandler = null,
        Func<UpdateCartLineQuantityRequest, CancellationToken, Task<SalesCartResult>>? updateHandler = null,
        Func<ProductId, SalesCartResult>? removeHandler = null,
        Func<SalesCartResult>? clearHandler = null)
    {
        _searchHandler = searchHandler;
        _addHandler = addHandler;
        _updateHandler = updateHandler;
        _removeHandler = removeHandler;
        _clearHandler = clearHandler;
    }

    public int SearchCallCount { get; private set; }

    public int AddCallCount { get; private set; }

    public int UpdateCallCount { get; private set; }

    public int RemoveCallCount { get; private set; }

    public int ClearCallCount { get; private set; }

    public string? LastSearchTerm { get; private set; }

    public AddProductToCartRequest? LastAddRequest { get; private set; }

    public UpdateCartLineQuantityRequest? LastUpdateRequest { get; private set; }

    public ProductId? LastRemovedProductId { get; private set; }

    public Task<IReadOnlyList<ProductSearchResult>> SearchProductsAsync(
        string searchTerm, CancellationToken cancellationToken = default)
    {
        SearchCallCount++;
        LastSearchTerm = searchTerm;

        return _searchHandler is null
            ? Task.FromResult<IReadOnlyList<ProductSearchResult>>(Array.Empty<ProductSearchResult>())
            : _searchHandler(searchTerm, cancellationToken);
    }

    public Task<SalesCartResult> AddProductAsync(
        AddProductToCartRequest request, CancellationToken cancellationToken = default)
    {
        AddCallCount++;
        LastAddRequest = request;

        return _addHandler is null
            ? Task.FromResult(SalesCartResult.Failure(SalesCartResultStatus.ProductNotFound))
            : _addHandler(request, cancellationToken);
    }

    public Task<SalesCartResult> UpdateQuantityAsync(
        UpdateCartLineQuantityRequest request, CancellationToken cancellationToken = default)
    {
        UpdateCallCount++;
        LastUpdateRequest = request;

        return _updateHandler is null
            ? Task.FromResult(SalesCartResult.Failure(SalesCartResultStatus.LineNotFound))
            : _updateHandler(request, cancellationToken);
    }

    public SalesCartResult RemoveLine(ProductId productId)
    {
        RemoveCallCount++;
        LastRemovedProductId = productId;

        return _removeHandler is null
            ? SalesCartResult.SuccessResult(SalesCartSnapshot.Empty("MXN"))
            : _removeHandler(productId);
    }

    public SalesCartResult Clear()
    {
        ClearCallCount++;

        return _clearHandler is null
            ? SalesCartResult.SuccessResult(SalesCartSnapshot.Empty("MXN"))
            : _clearHandler();
    }
}
