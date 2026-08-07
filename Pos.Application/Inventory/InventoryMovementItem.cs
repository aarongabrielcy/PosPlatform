using Pos.Domain.Common.Identifiers;
using Pos.Domain.Inventory;

namespace Pos.Application.Inventory;

// Proyección de InventoryMovement+Product para una fila del historial (TAREA 24G, sección 21/22):
// expone únicamente campos que InventoryMovement persiste realmente (QuantityBefore/QuantityAfter,
// no reconstruidos). SaleId sirve como "Referencia" para movimientos de venta.
public sealed class InventoryMovementItem
{
    public InventoryMovementId Id { get; }

    public ProductId ProductId { get; }

    public string Sku { get; }

    public string ProductName { get; }

    public InventoryMovementType Type { get; }

    public decimal Quantity { get; }

    public decimal QuantityBefore { get; }

    public decimal QuantityAfter { get; }

    public SaleId? SaleId { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    public InventoryMovementItem(
        InventoryMovementId id,
        ProductId productId,
        string sku,
        string productName,
        InventoryMovementType type,
        decimal quantity,
        decimal quantityBefore,
        decimal quantityAfter,
        SaleId? saleId,
        DateTimeOffset occurredAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentException.ThrowIfNullOrWhiteSpace(productName);

        Id = id;
        ProductId = productId;
        Sku = sku;
        ProductName = productName;
        Type = type;
        Quantity = quantity;
        QuantityBefore = quantityBefore;
        QuantityAfter = quantityAfter;
        SaleId = saleId;
        OccurredAtUtc = occurredAtUtc;
    }
}
