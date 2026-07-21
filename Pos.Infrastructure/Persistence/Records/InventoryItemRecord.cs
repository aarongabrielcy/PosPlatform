namespace Pos.Infrastructure.Persistence.Records;

internal sealed class InventoryItemRecord
{
    public Guid Id { get; set; }

    public Guid BranchId { get; set; }

    public Guid ProductId { get; set; }

    public decimal Quantity { get; set; }

    public decimal ReorderPoint { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
