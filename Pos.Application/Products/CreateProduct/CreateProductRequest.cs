namespace Pos.Application.Products.CreateProduct;

// SalePrice/Cost se interpretan en la moneda de la caja actual (ICurrentRegisterSession); no se
// acepta Currency desde Desktop. OrganizationId/BranchId/UserId tampoco se aceptan desde Desktop:
// se derivan de ICurrentUserSession/ICurrentRegisterSession dentro de CreateProductService.
public sealed record CreateProductRequest(
    string Sku,
    string? Barcode,
    string Name,
    string? Description,
    decimal SalePrice,
    decimal? Cost,
    bool TracksInventory,
    decimal InitialQuantity,
    decimal ReorderPoint);
