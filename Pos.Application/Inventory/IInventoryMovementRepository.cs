using Pos.Domain.Inventory;

namespace Pos.Application.Inventory;

public interface IInventoryMovementRepository
{
    Task AddAsync(InventoryMovement movement, CancellationToken cancellationToken);
}
