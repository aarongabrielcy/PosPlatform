using Pos.Domain.Common.Identifiers;
using Pos.Domain.Inventory;

namespace Pos.Application.Products.ManageProduct;

// BranchId no se acepta desde Desktop: se deriva de ICurrentRegisterSession (la caja actual)
// dentro de ProductManagementService, igual que en CreateProductService/SalesCartService.
// Quantity es siempre la magnitud del ajuste (positiva); AdjustmentType decide si incrementa o
// decrementa la existencia actual.
public sealed record AdjustProductInventoryRequest(
    ProductId ProductId,
    InventoryAdjustmentType AdjustmentType,
    decimal Quantity);
