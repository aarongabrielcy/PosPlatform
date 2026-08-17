namespace Pos.Application.Sales.Checkout;

// Resultado de un checkout exitoso. Solo expone tipos primitivos/Application (igual que
// ActiveRegisterSession y SalesCartSnapshot): Pos.Desktop no depende de Pos.Domain y nunca debe
// recibir SaleId/Money. Para PaymentMethod Card, CashTendered/ChangeAmount siempre son cero (no
// aplica efectivo) y CardReference lleva la referencia/autorización registrada; para Cash,
// CardReference siempre es null.
public sealed class CheckoutSummary
{
    public Guid SaleId { get; }

    public DateTimeOffset CompletedAtUtc { get; }

    public decimal TotalAmount { get; }

    public string Currency { get; }

    public CheckoutPaymentMethod PaymentMethod { get; }

    public decimal CashTendered { get; }

    public decimal ChangeAmount { get; }

    public string? CardReference { get; }

    public CheckoutSummary(
        Guid saleId,
        DateTimeOffset completedAtUtc,
        decimal totalAmount,
        string currency,
        CheckoutPaymentMethod paymentMethod,
        decimal cashTendered,
        decimal changeAmount,
        string? cardReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        SaleId = saleId;
        CompletedAtUtc = completedAtUtc;
        TotalAmount = totalAmount;
        Currency = currency;
        PaymentMethod = paymentMethod;
        CashTendered = cashTendered;
        ChangeAmount = changeAmount;
        CardReference = cardReference;
    }
}
