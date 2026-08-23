using Pos.Application.Products.ManageProduct;

namespace Pos.Infrastructure.Tests.Products;

internal sealed class FakeProductImageStore : IProductImageStore
{
    public Task<ProductImageStoreResult> SaveAsync(byte[] content, CancellationToken cancellationToken = default) =>
        Task.FromResult(ProductImageStoreResult.SuccessResult($"{Guid.NewGuid():N}.jpg"));

    public Task DeleteAsync(string imageFileName, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
