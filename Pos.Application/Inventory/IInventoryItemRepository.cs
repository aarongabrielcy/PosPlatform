using Pos.Domain.Common.Identifiers;
using Pos.Domain.Inventory;

namespace Pos.Application.Inventory;

public interface IInventoryItemRepository
{
    Task<InventoryItem?> GetByBranchAndProductAsync(
        BranchId branchId,
        ProductId productId,
        CancellationToken cancellationToken);

    Task AddAsync(InventoryItem inventoryItem, CancellationToken cancellationToken);

    Task UpdateAsync(InventoryItem inventoryItem, CancellationToken cancellationToken);
}
