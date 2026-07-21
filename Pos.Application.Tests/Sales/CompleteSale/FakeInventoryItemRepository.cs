using Pos.Application.Inventory;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Inventory;

namespace Pos.Application.Tests.Sales.CompleteSale;

internal sealed class FakeInventoryItemRepository : IInventoryItemRepository
{
    private readonly Dictionary<(BranchId BranchId, ProductId ProductId), InventoryItem> _items = new();
    private readonly List<string>? _operationLog;

    public FakeInventoryItemRepository(List<string>? operationLog = null)
    {
        _operationLog = operationLog;
    }

    public List<InventoryItem> UpdatedItems { get; } = new();

    public int GetByBranchAndProductCallCount { get; private set; }

    public int UpdateCallCount { get; private set; }

    public void Add(InventoryItem item) => _items[(item.BranchId, item.ProductId)] = item;

    public void AddAt(BranchId branchId, ProductId productId, InventoryItem item) =>
        _items[(branchId, productId)] = item;

    public Task<InventoryItem?> GetByBranchAndProductAsync(
        BranchId branchId,
        ProductId productId,
        CancellationToken cancellationToken)
    {
        GetByBranchAndProductCallCount++;

        return Task.FromResult(_items.TryGetValue((branchId, productId), out var item) ? item : null);
    }

    public Task UpdateAsync(InventoryItem inventoryItem, CancellationToken cancellationToken)
    {
        UpdateCallCount++;
        UpdatedItems.Add(inventoryItem);
        _operationLog?.Add("Inventory.Update");

        return Task.CompletedTask;
    }
}
