using Pos.Application.Products.CreateProduct;

namespace Pos.Desktop.Tests.Products;

internal sealed class FakeCreateProductService : ICreateProductService
{
    private readonly Func<CreateProductRequest, CancellationToken, Task<CreateProductResult>>? _createHandler;

    public FakeCreateProductService(
        Func<CreateProductRequest, CancellationToken, Task<CreateProductResult>>? createHandler = null)
    {
        _createHandler = createHandler;
    }

    public int CreateCallCount { get; private set; }

    public CreateProductRequest? LastRequest { get; private set; }

    public Task<CreateProductResult> CreateAsync(
        CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        CreateCallCount++;
        LastRequest = request;

        return _createHandler is null
            ? Task.FromResult(CreateProductResult.Failure(CreateProductResultStatus.InvalidSku))
            : _createHandler(request, cancellationToken);
    }
}
