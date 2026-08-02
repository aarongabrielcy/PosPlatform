namespace Pos.Application.Products.ManageProduct;

// Resultado compartido por UpdateAsync y SetActiveAsync: ambas operaciones terminan en el mismo
// tipo de estado (éxito con el detalle actualizado, o alguno de los mismos motivos de rechazo).
public sealed class UpdateProductResult
{
    public UpdateProductResultStatus Status { get; }

    public bool Success => Status == UpdateProductResultStatus.Success;

    public ProductDetails? Product { get; }

    private UpdateProductResult(UpdateProductResultStatus status, ProductDetails? product)
    {
        Status = status;
        Product = product;
    }

    public static UpdateProductResult SuccessResult(ProductDetails product)
    {
        ArgumentNullException.ThrowIfNull(product);

        return new UpdateProductResult(UpdateProductResultStatus.Success, product);
    }

    public static UpdateProductResult Failure(UpdateProductResultStatus status)
    {
        if (status == UpdateProductResultStatus.Success)
        {
            throw new ArgumentException(
                "Success requiere ProductDetails; use SuccessResult.", nameof(status));
        }

        return new UpdateProductResult(status, null);
    }
}
