namespace Pos.Application.Sales.Checkout;

// Resultado de un checkout exitoso. Solo expone tipos primitivos (igual que ActiveRegisterSession
// y SalesCartSnapshot): Pos.Desktop no depende de Pos.Domain y nunca debe recibir SaleId/Money.
public sealed class CheckoutSummary
{
    public Guid SaleId { get; }

    public DateTimeOffset CompletedAtUtc { get; }

    public decimal TotalAmount { get; }

    public string Currency { get; }

    public decimal CashTendered { get; }

    public decimal ChangeAmount { get; }

    public CheckoutSummary(
        Guid saleId,
        DateTimeOffset completedAtUtc,
        decimal totalAmount,
        string currency,
        decimal cashTendered,
        decimal changeAmount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        SaleId = saleId;
        CompletedAtUtc = completedAtUtc;
        TotalAmount = totalAmount;
        Currency = currency;
        CashTendered = cashTendered;
        ChangeAmount = changeAmount;
    }
}
