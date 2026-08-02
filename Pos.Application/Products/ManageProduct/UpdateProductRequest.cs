using Pos.Domain.Common.Identifiers;

namespace Pos.Application.Products.ManageProduct;

// SalePrice/Cost se interpretan en la moneda ya asignada al Product existente; no se acepta
// Currency desde Desktop. Sku y TracksInventory no aparecen aquí: permanecen read-only en esta
// fase (ver inspección de TAREA 24B). ReorderPoint es null cuando el producto no controla
// inventario o cuando la UI no lo modifica; ProductManagementService lo ignora si el producto no
// controla inventario, sin crear InventoryItem.
public sealed record UpdateProductRequest(
    ProductId ProductId,
    string? Barcode,
    string Name,
    string? Description,
    decimal SalePrice,
    decimal? Cost,
    decimal? ReorderPoint);
