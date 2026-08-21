namespace Pos.Application.CashMovements;

public sealed class CashMovementListResult
{
    public CashMovementResultStatus Status { get; }

    public bool Success => Status == CashMovementResultStatus.Success;

    public IReadOnlyList<CashMovementEntry>? Movements { get; }

    private CashMovementListResult(CashMovementResultStatus status, IReadOnlyList<CashMovementEntry>? movements)
    {
        Status = status;
        Movements = movements;
    }

    public static CashMovementListResult SuccessResult(IReadOnlyList<CashMovementEntry> movements)
    {
        ArgumentNullException.ThrowIfNull(movements);

        return new CashMovementListResult(CashMovementResultStatus.Success, movements);
    }

    public static CashMovementListResult Failure(CashMovementResultStatus status)
    {
        if (status == CashMovementResultStatus.Success)
        {
            throw new ArgumentException(
                "Success requiere Movements; use SuccessResult.", nameof(status));
        }

        return new CashMovementListResult(status, null);
    }
}
