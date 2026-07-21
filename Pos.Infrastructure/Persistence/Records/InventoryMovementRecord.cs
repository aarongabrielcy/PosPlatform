using Pos.Domain.Inventory;

namespace Pos.Infrastructure.Persistence.Records;

internal sealed class InventoryMovementRecord
{
    public Guid Id { get; set; }

    public Guid InventoryItemId { get; set; }

    public Guid BranchId { get; set; }

    public Guid ProductId { get; set; }

    public Guid PerformedByUserId { get; set; }

    public InventoryMovementType Type { get; set; }

    public decimal Quantity { get; set; }

    public decimal QuantityBefore { get; set; }

    public decimal QuantityAfter { get; set; }

    public Guid? SaleId { get; set; }

    public Guid? SaleLineId { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }
}
