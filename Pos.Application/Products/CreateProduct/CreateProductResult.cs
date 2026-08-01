using Pos.Domain.Common.Identifiers;

namespace Pos.Application.Products.CreateProduct;

public sealed class CreateProductResult
{
    public CreateProductResultStatus Status { get; }

    public bool Success => Status == CreateProductResultStatus.Success;

    public ProductId? ProductId { get; }

    public string? Sku { get; }

    private CreateProductResult(CreateProductResultStatus status, ProductId? productId, string? sku)
    {
        Status = status;
        ProductId = productId;
        Sku = sku;
    }

    public static CreateProductResult SuccessResult(ProductId productId, string sku)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);

        return new CreateProductResult(CreateProductResultStatus.Success, productId, sku);
    }

    public static CreateProductResult Failure(CreateProductResultStatus status)
    {
        if (status == CreateProductResultStatus.Success)
        {
            throw new ArgumentException(
                "Success requiere ProductId y Sku; use SuccessResult.", nameof(status));
        }

        return new CreateProductResult(status, null, null);
    }
}
