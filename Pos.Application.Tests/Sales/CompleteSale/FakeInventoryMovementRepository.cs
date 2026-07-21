using Pos.Application.Inventory;
using Pos.Domain.Inventory;

namespace Pos.Application.Tests.Sales.CompleteSale;

internal sealed class FakeInventoryMovementRepository : IInventoryMovementRepository
{
    private readonly List<string>? _operationLog;

    public FakeInventoryMovementRepository(List<string>? operationLog = null)
    {
        _operationLog = operationLog;
    }

    public List<InventoryMovement> AddedMovements { get; } = new();

    public int AddCallCount { get; private set; }

    public Task AddAsync(InventoryMovement movement, CancellationToken cancellationToken)
    {
        AddCallCount++;
        AddedMovements.Add(movement);
        _operationLog?.Add("Movement.Add");

        return Task.CompletedTask;
    }
}
