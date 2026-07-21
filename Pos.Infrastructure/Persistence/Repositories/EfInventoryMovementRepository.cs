using Pos.Application.Inventory;
using Pos.Domain.Inventory;
using Pos.Infrastructure.Persistence.Mappers;

namespace Pos.Infrastructure.Persistence.Repositories;

public sealed class EfInventoryMovementRepository : IInventoryMovementRepository
{
    private readonly PosDbContext _context;

    public EfInventoryMovementRepository(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task AddAsync(InventoryMovement movement, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(movement);

        var record = InventoryMovementMapper.ToRecord(movement);

        await _context.InventoryMovements.AddAsync(record, cancellationToken);
    }
}
