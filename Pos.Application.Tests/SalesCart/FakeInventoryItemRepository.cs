using Pos.Application.Inventory;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Inventory;

namespace Pos.Application.Tests.SalesCart;

internal sealed class FakeInventoryItemRepository : IInventoryItemRepository
{
    private readonly Dictionary<(BranchId BranchId, ProductId ProductId), InventoryItem> _items = new();

    public int GetByBranchAndProductCallCount { get; private set; }

    public void Add(InventoryItem item) => _items[(item.BranchId, item.ProductId)] = item;

    public Task AddAsync(InventoryItem inventoryItem, CancellationToken cancellationToken)
    {
        _items[(inventoryItem.BranchId, inventoryItem.ProductId)] = inventoryItem;

        return Task.CompletedTask;
    }

    public Task<InventoryItem?> GetByBranchAndProductAsync(
        BranchId branchId, ProductId productId, CancellationToken cancellationToken)
    {
        GetByBranchAndProductCallCount++;

        return Task.FromResult(_items.TryGetValue((branchId, productId), out var item) ? item : null);
    }

    public Task UpdateAsync(InventoryItem inventoryItem, CancellationToken cancellationToken)
    {
        _items[(inventoryItem.BranchId, inventoryItem.ProductId)] = inventoryItem;

        return Task.CompletedTask;
    }
}
