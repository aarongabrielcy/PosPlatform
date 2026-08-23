namespace Pos.Application.Products.ManageProduct;

public enum UpdateProductResultStatus
{
    Success,
    NotAuthenticated,
    NotAuthorized,
    InstallationRestricted,
    ProductNotFound,
    InvalidName,
    InvalidSku,
    InvalidBarcode,
    InvalidSalePrice,
    InvalidCost,
    InvalidReorderPoint,
    DuplicateSku,
    DuplicateBarcode,

    // BASIC-UX-01: usados exclusivamente por SetProductImageAsync.
    InvalidImage,
    ImageTooLarge,
}
