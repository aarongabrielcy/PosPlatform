using Pos.Application.Products.ManageProduct;

namespace Pos.Application.Tests.Products.ManageProduct;

internal sealed class FakeProductImageStore : IProductImageStore
{
    private readonly HashSet<string> _savedFiles = new();
    private readonly List<string> _deletedFiles = new();

    public ProductImageStoreStatus NextSaveStatus { get; set; } = ProductImageStoreStatus.Success;

    public int SaveCallCount { get; private set; }

    public int DeleteCallCount { get; private set; }

    public IReadOnlyList<string> DeletedFiles => _deletedFiles;

    public Task<ProductImageStoreResult> SaveAsync(byte[] content, CancellationToken cancellationToken = default)
    {
        SaveCallCount++;

        if (NextSaveStatus != ProductImageStoreStatus.Success)
        {
            return Task.FromResult(ProductImageStoreResult.Failure(NextSaveStatus));
        }

        var fileName = $"{Guid.NewGuid():N}.jpg";
        _savedFiles.Add(fileName);

        return Task.FromResult(ProductImageStoreResult.SuccessResult(fileName));
    }

    public Task DeleteAsync(string imageFileName, CancellationToken cancellationToken = default)
    {
        DeleteCallCount++;
        _deletedFiles.Add(imageFileName);
        _savedFiles.Remove(imageFileName);

        return Task.CompletedTask;
    }
}
