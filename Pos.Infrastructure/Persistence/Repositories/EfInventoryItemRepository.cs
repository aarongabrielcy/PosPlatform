using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Exceptions;
using Pos.Application.Inventory;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Inventory;
using Pos.Infrastructure.Persistence.Mappers;

namespace Pos.Infrastructure.Persistence.Repositories;

public sealed class EfInventoryItemRepository : IInventoryItemRepository
{
    private readonly PosDbContext _context;

    public EfInventoryItemRepository(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<InventoryItem?> GetByBranchAndProductAsync(
        BranchId branchId,
        ProductId productId,
        CancellationToken cancellationToken)
    {
        var record = await _context.InventoryItems
            .AsNoTracking()
            .SingleOrDefaultAsync(
                r => r.BranchId == branchId.Value && r.ProductId == productId.Value,
                cancellationToken);

        return record is null ? null : InventoryItemMapper.ToDomain(record);
    }

    public async Task UpdateAsync(InventoryItem inventoryItem, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inventoryItem);

        var record = await _context.InventoryItems
            .SingleOrDefaultAsync(r => r.Id == inventoryItem.Id.Value, cancellationToken);

        if (record is null)
        {
            throw new EntityNotFoundException("InventoryItem", inventoryItem.Id.ToString());
        }

        InventoryItemMapper.UpdateRecord(inventoryItem, record);
    }
}
