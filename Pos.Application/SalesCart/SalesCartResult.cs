namespace Pos.Application.SalesCart;

public sealed class SalesCartResult
{
    public SalesCartResultStatus Status { get; }

    public bool Success => Status == SalesCartResultStatus.Success;

    public SalesCartSnapshot? Snapshot { get; }

    // Solo tiene valor en fallas de existencia (OutOfStock/InsufficientStock): permite que la UI
    // muestre cuánto hay disponible sin que Desktop tenga que volver a consultar el servicio.
    public decimal? AvailableQuantity { get; }

    private SalesCartResult(SalesCartResultStatus status, SalesCartSnapshot? snapshot, decimal? availableQuantity)
    {
        Status = status;
        Snapshot = snapshot;
        AvailableQuantity = availableQuantity;
    }

    public static SalesCartResult SuccessResult(SalesCartSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new SalesCartResult(SalesCartResultStatus.Success, snapshot, null);
    }

    public static SalesCartResult Failure(SalesCartResultStatus status)
    {
        if (status == SalesCartResultStatus.Success)
        {
            throw new ArgumentException("Success requiere Snapshot; use SuccessResult.", nameof(status));
        }

        return new SalesCartResult(status, null, null);
    }

    public static SalesCartResult Failure(SalesCartResultStatus status, decimal availableQuantity)
    {
        if (status == SalesCartResultStatus.Success)
        {
            throw new ArgumentException("Success requiere Snapshot; use SuccessResult.", nameof(status));
        }

        return new SalesCartResult(status, null, availableQuantity);
    }
}
