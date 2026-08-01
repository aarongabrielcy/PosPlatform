using Pos.Application.Inventory;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Inventory;

namespace Pos.Application.Tests.Products.CreateProduct;

internal sealed class FakeInventoryItemRepository : IInventoryItemRepository
{
    private readonly Dictionary<(BranchId BranchId, ProductId ProductId), InventoryItem> _items = new();

    public int AddCallCount { get; private set; }

    public Task<InventoryItem?> GetByBranchAndProductAsync(
        BranchId branchId, ProductId productId, CancellationToken cancellationToken) =>
        Task.FromResult(_items.TryGetValue((branchId, productId), out var item) ? item : null);

    public Task AddAsync(InventoryItem inventoryItem, CancellationToken cancellationToken)
    {
        AddCallCount++;
        _items[(inventoryItem.BranchId, inventoryItem.ProductId)] = inventoryItem;

        return Task.CompletedTask;
    }

    public Task UpdateAsync(InventoryItem inventoryItem, CancellationToken cancellationToken)
    {
        _items[(inventoryItem.BranchId, inventoryItem.ProductId)] = inventoryItem;

        return Task.CompletedTask;
    }
}
