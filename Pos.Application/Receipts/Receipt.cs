namespace Pos.Application.Receipts;

// Modelo de ticket comercial (BASIC-PRN-01). NO es CFDI: no incluye RFC/IVA/folio fiscal/UUID/
// timbrado. Se construye siempre a partir de datos ya persistidos (SaleHistoryDetail), nunca de
// Product/User/Register actuales, de modo que un reimpreso reproduce exactamente lo que se cobró
// (ver ReceiptBuilder). CashTendered/ChangeDue son la única excepción: Payment no los persiste
// (son transitorios del checkout, ver CheckoutService), así que solo tienen valor en la impresión
// inicial inmediatamente posterior al cobro (nunca en un reimpreso desde Historial).
public sealed class Receipt
{
    public string SaleReference { get; }

    public DateTimeOffset CompletedAtUtc { get; }

    public string BusinessName { get; }

    public string RegisterName { get; }

    public string CashierDisplayName { get; }

    public IReadOnlyList<ReceiptLine> Lines { get; }

    public decimal Subtotal { get; }

    public decimal Total { get; }

    public string Currency { get; }

    public IReadOnlyList<ReceiptPayment> Payments { get; }

    public decimal? CashTendered { get; }

    public decimal? ChangeDue { get; }

    // Distingue un ticket impreso justo tras el cobro de un reimpreso desde Historial (sección 24):
    // el formateador debe marcar visiblemente "REIMPRESIÓN" solo cuando esto es true.
    public bool IsReprint { get; }

    public Receipt(
        string saleReference,
        DateTimeOffset completedAtUtc,
        string businessName,
        string registerName,
        string cashierDisplayName,
        IReadOnlyList<ReceiptLine> lines,
        decimal subtotal,
        decimal total,
        string currency,
        IReadOnlyList<ReceiptPayment> payments,
        decimal? cashTendered,
        decimal? changeDue,
        bool isReprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(saleReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(businessName);
        ArgumentException.ThrowIfNullOrWhiteSpace(registerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(cashierDisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(payments);

        SaleReference = saleReference;
        CompletedAtUtc = completedAtUtc;
        BusinessName = businessName;
        RegisterName = registerName;
        CashierDisplayName = cashierDisplayName;
        Lines = lines;
        Subtotal = subtotal;
        Total = total;
        Currency = currency;
        Payments = payments;
        CashTendered = cashTendered;
        ChangeDue = changeDue;
        IsReprint = isReprint;
    }
}
