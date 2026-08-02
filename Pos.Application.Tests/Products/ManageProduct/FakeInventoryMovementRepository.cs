using Pos.Application.Inventory;
using Pos.Domain.Inventory;

namespace Pos.Application.Tests.Products.ManageProduct;

internal sealed class FakeInventoryMovementRepository : IInventoryMovementRepository
{
    public List<InventoryMovement> AddedMovements { get; } = new();

    public int AddCallCount { get; private set; }

    public Task AddAsync(InventoryMovement movement, CancellationToken cancellationToken)
    {
        AddCallCount++;
        AddedMovements.Add(movement);

        return Task.CompletedTask;
    }
}
