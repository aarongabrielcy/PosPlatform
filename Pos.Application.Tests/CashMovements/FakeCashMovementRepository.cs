using Pos.Application.CashMovements;
using Pos.Domain.CashMovements;
using Pos.Domain.Common.Identifiers;

namespace Pos.Application.Tests.CashMovements;

internal sealed class FakeCashMovementRepository : ICashMovementRepository
{
    private readonly List<CashMovement> _movements;

    public FakeCashMovementRepository(IEnumerable<CashMovement>? seed = null)
    {
        _movements = seed?.ToList() ?? [];
    }

    public int AddCallCount { get; private set; }

    public CashMovement? LastAdded { get; private set; }

    public IReadOnlyList<CashMovement> Movements => _movements;

    public Task AddAsync(CashMovement movement, CancellationToken cancellationToken)
    {
        AddCallCount++;
        LastAdded = movement;
        _movements.Add(movement);

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CashMovement>> GetByRegisterSessionAsync(
        RegisterSessionId registerSessionId, CancellationToken cancellationToken)
    {
        IReadOnlyList<CashMovement> result = _movements
            .Where(m => m.RegisterSessionId == registerSessionId)
            .OrderBy(m => m.CreatedAtUtc)
            .ToList();

        return Task.FromResult(result);
    }

    public Task<decimal> GetCashInTotalByRegisterSessionAsync(
        RegisterSessionId registerSessionId, CancellationToken cancellationToken) =>
        Task.FromResult(SumByType(registerSessionId, CashMovementType.CashIn));

    public Task<decimal> GetCashOutTotalByRegisterSessionAsync(
        RegisterSessionId registerSessionId, CancellationToken cancellationToken) =>
        Task.FromResult(SumByType(registerSessionId, CashMovementType.CashOut));

    private decimal SumByType(RegisterSessionId registerSessionId, CashMovementType type) =>
        _movements
            .Where(m => m.RegisterSessionId == registerSessionId && m.Type == type)
            .Sum(m => m.Amount.Amount);
}
