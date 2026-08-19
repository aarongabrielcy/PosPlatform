namespace Pos.Application.Products.ManageProduct;

public enum AdjustProductInventoryResultStatus
{
    Success,
    NotAuthenticated,
    NotAuthorized,
    InstallationRestricted,
    RegisterSessionRequired,
    ProductNotFound,
    ProductDoesNotTrackInventory,
    InventoryItemNotFound,
    InvalidQuantity,
    ResultingQuantityNegative,
}
