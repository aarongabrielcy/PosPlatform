namespace Pos.Application.Products.ManageProduct;

public enum AdjustProductInventoryResultStatus
{
    Success,
    NotAuthenticated,
    NotAuthorized,
    RegisterSessionRequired,
    ProductNotFound,
    ProductDoesNotTrackInventory,
    InventoryItemNotFound,
    InvalidQuantity,
    ResultingQuantityNegative,
}
