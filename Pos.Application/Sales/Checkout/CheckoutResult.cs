namespace Pos.Application.Sales.Checkout;

public sealed class CheckoutResult
{
    public CheckoutResultStatus Status { get; }

    public bool Success => Status == CheckoutResultStatus.Success;

    public CheckoutSummary? Summary { get; }

    // Solo tiene valor en InsufficientStock: permite que la UI muestre cuánto hay disponible sin
    // volver a consultar el servicio (mismo patrón que SalesCartResult.AvailableQuantity).
    public decimal? AvailableQuantity { get; }

    // Solo tienen valor en ProductChanged (TAREA 25A sección 8): identifican el producto y el
    // dato que cambió, para que la UI pueda construir un mensaje sin volver a consultar.
    public Guid? ChangedProductId { get; }

    public CheckoutProductChangeReason? ChangedProductReason { get; }

    public string? ChangedProductCurrentValue { get; }

    private CheckoutResult(
        CheckoutResultStatus status,
        CheckoutSummary? summary,
        decimal? availableQuantity,
        Guid? changedProductId,
        CheckoutProductChangeReason? changedProductReason,
        string? changedProductCurrentValue)
    {
        Status = status;
        Summary = summary;
        AvailableQuantity = availableQuantity;
        ChangedProductId = changedProductId;
        ChangedProductReason = changedProductReason;
        ChangedProductCurrentValue = changedProductCurrentValue;
    }

    public static CheckoutResult SuccessResult(CheckoutSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return new CheckoutResult(CheckoutResultStatus.Success, summary, null, null, null, null);
    }

    public static CheckoutResult Failure(CheckoutResultStatus status)
    {
        if (status == CheckoutResultStatus.Success)
        {
            throw new ArgumentException("Success requiere Summary; use SuccessResult.", nameof(status));
        }

        return new CheckoutResult(status, null, null, null, null, null);
    }

    public static CheckoutResult InsufficientStockResult(decimal availableQuantity) =>
        new(CheckoutResultStatus.InsufficientStock, null, availableQuantity, null, null, null);

    public static CheckoutResult ProductChangedResult(
        Guid productId, CheckoutProductChangeReason reason, string currentValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentValue);

        return new CheckoutResult(CheckoutResultStatus.ProductChanged, null, null, productId, reason, currentValue);
    }
}
