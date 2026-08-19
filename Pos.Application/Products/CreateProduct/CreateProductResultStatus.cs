namespace Pos.Application.Products.CreateProduct;

public enum CreateProductResultStatus
{
    Success,
    NotAuthenticated,
    NotAuthorized,
    InstallationRestricted,
    RegisterSessionRequired,
    InvalidSku,
    InvalidName,
    InvalidBarcode,
    InvalidSalePrice,
    InvalidCost,
    InvalidInitialQuantity,
    InvalidReorderPoint,
    DuplicateSku,
    DuplicateBarcode,
}
