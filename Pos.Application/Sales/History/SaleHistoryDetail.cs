using Pos.Domain.Common.Identifiers;
using Pos.Domain.Sales;

namespace Pos.Application.Sales.History;

// Detalle histórico completo de una venta (TAREA 25B, sección 6/23). CashierDisplayName y
// RegisterName provienen de un JOIN a los registros actuales de User/Register, no de un snapshot
// (sección 26/27): si el usuario o el register cambiaron de nombre después de la venta, aquí se
// mostrará el nombre actual, no el histórico. Lines/Payments sí son snapshots reales.
public sealed class SaleHistoryDetail
{
    public SaleId SaleId { get; }

    public SaleStatus Status { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset? CompletedAtUtc { get; }

    public UserId CashierUserId { get; }

    public string CashierDisplayName { get; }

    public RegisterId RegisterId { get; }

    public string RegisterName { get; }

    public RegisterSessionId RegisterSessionId { get; }

    public decimal Subtotal { get; }

    public decimal Total { get; }

    public string Currency { get; }

    public IReadOnlyList<SaleHistoryDetailLine> Lines { get; }

    public IReadOnlyList<SaleHistoryDetailPayment> Payments { get; }

    public SaleHistoryDetail(
        SaleId saleId,
        SaleStatus status,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? completedAtUtc,
        UserId cashierUserId,
        string cashierDisplayName,
        RegisterId registerId,
        string registerName,
        RegisterSessionId registerSessionId,
        decimal subtotal,
        decimal total,
        string currency,
        IReadOnlyList<SaleHistoryDetailLine> lines,
        IReadOnlyList<SaleHistoryDetailPayment> payments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cashierDisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(registerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(payments);

        SaleId = saleId;
        Status = status;
        CreatedAtUtc = createdAtUtc;
        CompletedAtUtc = completedAtUtc;
        CashierUserId = cashierUserId;
        CashierDisplayName = cashierDisplayName;
        RegisterId = registerId;
        RegisterName = registerName;
        RegisterSessionId = registerSessionId;
        Subtotal = subtotal;
        Total = total;
        Currency = currency;
        Lines = lines;
        Payments = payments;
    }
}
