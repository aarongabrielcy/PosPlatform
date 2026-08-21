using Pos.Application.CashMovements;

namespace Pos.Desktop.Tests.Register;

internal sealed class FakeCashMovementService : ICashMovementService
{
    public Func<RecordCashMovementRequest, Task<CashMovementResult>>? RecordCashInHandler { get; set; }

    public Func<RecordCashMovementRequest, Task<CashMovementResult>>? RecordCashOutHandler { get; set; }

    public Func<Task<CashMovementListResult>>? GetCurrentSessionMovementsHandler { get; set; }

    public int GetCurrentSessionMovementsCallCount { get; private set; }

    public Task<CashMovementResult> RecordCashInAsync(
        RecordCashMovementRequest request, CancellationToken cancellationToken = default) =>
        RecordCashInHandler?.Invoke(request) ??
        Task.FromResult(CashMovementResult.Failure(CashMovementResultStatus.SessionNotFound));

    public Task<CashMovementResult> RecordCashOutAsync(
        RecordCashMovementRequest request, CancellationToken cancellationToken = default) =>
        RecordCashOutHandler?.Invoke(request) ??
        Task.FromResult(CashMovementResult.Failure(CashMovementResultStatus.SessionNotFound));

    public Task<CashMovementListResult> GetCurrentSessionMovementsAsync(CancellationToken cancellationToken = default)
    {
        GetCurrentSessionMovementsCallCount++;

        return GetCurrentSessionMovementsHandler?.Invoke() ??
            Task.FromResult(CashMovementListResult.SuccessResult([]));
    }
}
