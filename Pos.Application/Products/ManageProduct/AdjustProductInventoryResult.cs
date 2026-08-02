using Pos.Domain.Common.Identifiers;

namespace Pos.Application.Products.ManageProduct;

public sealed class AdjustProductInventoryResult
{
    public AdjustProductInventoryResultStatus Status { get; }

    public bool Success => Status == AdjustProductInventoryResultStatus.Success;

    public ProductId? ProductId { get; }

    public decimal? NewQuantity { get; }

    private AdjustProductInventoryResult(
        AdjustProductInventoryResultStatus status, ProductId? productId, decimal? newQuantity)
    {
        Status = status;
        ProductId = productId;
        NewQuantity = newQuantity;
    }

    public static AdjustProductInventoryResult SuccessResult(ProductId productId, decimal newQuantity) =>
        new(AdjustProductInventoryResultStatus.Success, productId, newQuantity);

    public static AdjustProductInventoryResult Failure(AdjustProductInventoryResultStatus status)
    {
        if (status == AdjustProductInventoryResultStatus.Success)
        {
            throw new ArgumentException(
                "Success requiere ProductId y NewQuantity; use SuccessResult.", nameof(status));
        }

        return new AdjustProductInventoryResult(status, null, null);
    }
}
