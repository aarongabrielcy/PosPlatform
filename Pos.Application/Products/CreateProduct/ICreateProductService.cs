namespace Pos.Application.Products.CreateProduct;

public interface ICreateProductService
{
    Task<CreateProductResult> CreateAsync(
        CreateProductRequest request, CancellationToken cancellationToken = default);
}
