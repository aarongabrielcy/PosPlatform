namespace Pos.Application.Products.ManageProduct;

public enum UpdateProductResultStatus
{
    Success,
    NotAuthenticated,
    NotAuthorized,
    ProductNotFound,
    InvalidName,
    InvalidBarcode,
    InvalidSalePrice,
    InvalidCost,
    InvalidReorderPoint,
    DuplicateBarcode,
}
