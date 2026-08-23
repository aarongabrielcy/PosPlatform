using Pos.Application.Products.ManageProduct;
using Pos.Domain.Common.Identifiers;

namespace Pos.Desktop.Tests.Products;

internal sealed class FakeProductPhotoService : IProductPhotoService
{
    private readonly Func<ProductId, byte[], CancellationToken, Task<UpdateProductResult>>? _setHandler;
    private readonly Func<ProductId, CancellationToken, Task<UpdateProductResult>>? _removeHandler;

    public FakeProductPhotoService(
        Func<ProductId, byte[], CancellationToken, Task<UpdateProductResult>>? setHandler = null,
        Func<ProductId, CancellationToken, Task<UpdateProductResult>>? removeHandler = null)
    {
        _setHandler = setHandler;
        _removeHandler = removeHandler;
    }

    public int SetProductImageCallCount { get; private set; }

    public int RemoveProductImageCallCount { get; private set; }

    public byte[]? LastImageContent { get; private set; }

    public Task<UpdateProductResult> SetProductImageAsync(
        ProductId productId, byte[] imageContent, CancellationToken cancellationToken = default)
    {
        SetProductImageCallCount++;
        LastImageContent = imageContent;

        return _setHandler is null
            ? Task.FromResult(UpdateProductResult.Failure(UpdateProductResultStatus.ProductNotFound))
            : _setHandler(productId, imageContent, cancellationToken);
    }

    public Task<UpdateProductResult> RemoveProductImageAsync(
        ProductId productId, CancellationToken cancellationToken = default)
    {
        RemoveProductImageCallCount++;

        return _removeHandler is null
            ? Task.FromResult(UpdateProductResult.Failure(UpdateProductResultStatus.ProductNotFound))
            : _removeHandler(productId, cancellationToken);
    }
}
