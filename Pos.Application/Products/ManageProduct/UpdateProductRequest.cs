using Pos.Domain.Common.Identifiers;

namespace Pos.Application.Products.ManageProduct;

// SalePrice/Cost se interpretan en la moneda ya asignada al Product existente; no se acepta
// Currency desde Desktop. TracksInventory no aparece aquí: permanece read-only (cambiarlo
// implicaría crear/eliminar InventoryItem, fuera de alcance). Sku es editable desde TAREA 24C
// (Product.ChangeSku); ProductId nunca cambia. ReorderPoint es null cuando el producto no
// controla inventario o cuando la UI no lo modifica; ProductManagementService lo ignora si el
// producto no controla inventario, sin crear InventoryItem.
public sealed record UpdateProductRequest(
    ProductId ProductId,
    string Sku,
    string? Barcode,
    string Name,
    string? Description,
    decimal SalePrice,
    decimal? Cost,
    decimal? ReorderPoint);
