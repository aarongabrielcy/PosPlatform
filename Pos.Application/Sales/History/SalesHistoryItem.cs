using Pos.Domain.Common.Identifiers;

namespace Pos.Application.Sales.History;

// Fila del listado Historial de ventas (TAREA 25B, sección 5/20). CashierDisplayName y
// RegisterName se resuelven por JOIN a User/Register actuales: Sale solo persiste
// CreatedByUserId/RegisterSessionId, sin snapshot de nombre (sección 26/27) — por eso este DTO
// nunca debe presentarse como snapshot histórico del nombre, solo del contenido de la venta.
public sealed class SalesHistoryItem
{
    public SaleId SaleId { get; }

    public DateTimeOffset CompletedAtUtc { get; }

    public UserId CashierUserId { get; }

    public string CashierDisplayName { get; }

    public RegisterId RegisterId { get; }

    public string RegisterName { get; }

    public RegisterSessionId RegisterSessionId { get; }

    // SUM(SaleLine.Quantity), no COUNT(Lines) (TAREA 25B, sección 19): Quantity puede ser > 1.
    public decimal ItemCount { get; }

    public decimal Total { get; }

    public string Currency { get; }

    public IReadOnlyList<SalesHistoryPaymentAmount> PaymentSummary { get; }

    public SalesHistoryItem(
        SaleId saleId,
        DateTimeOffset completedAtUtc,
        UserId cashierUserId,
        string cashierDisplayName,
        RegisterId registerId,
        string registerName,
        RegisterSessionId registerSessionId,
        decimal itemCount,
        decimal total,
        string currency,
        IReadOnlyList<SalesHistoryPaymentAmount> paymentSummary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cashierDisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(registerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        ArgumentNullException.ThrowIfNull(paymentSummary);

        SaleId = saleId;
        CompletedAtUtc = completedAtUtc;
        CashierUserId = cashierUserId;
        CashierDisplayName = cashierDisplayName;
        RegisterId = registerId;
        RegisterName = registerName;
        RegisterSessionId = registerSessionId;
        ItemCount = itemCount;
        Total = total;
        Currency = currency;
        PaymentSummary = paymentSummary;
    }
}
