namespace Pos.Application.CashMovements;

public sealed class CashMovementResult
{
    public CashMovementResultStatus Status { get; }

    public bool Success => Status == CashMovementResultStatus.Success;

    public CashMovementEntry? Movement { get; }

    private CashMovementResult(CashMovementResultStatus status, CashMovementEntry? movement)
    {
        Status = status;
        Movement = movement;
    }

    public static CashMovementResult SuccessResult(CashMovementEntry movement)
    {
        ArgumentNullException.ThrowIfNull(movement);

        return new CashMovementResult(CashMovementResultStatus.Success, movement);
    }

    public static CashMovementResult Failure(CashMovementResultStatus status)
    {
        if (status == CashMovementResultStatus.Success)
        {
            throw new ArgumentException(
                "Success requiere Movement; use SuccessResult.", nameof(status));
        }

        return new CashMovementResult(status, null);
    }
}
