using System.Globalization;
using System.Linq;
using Pos.Application.Sales.History;

namespace Pos.Desktop.Sales.History;

// Vista de Detalle dentro de Ventas > Historial (TAREA 25B, sección 23): envuelve SaleHistoryDetail
// con texto ya formateado. Read-only: no expone ningún comando de mutación (sección 50).
public sealed class SaleHistoryDetailViewModel
{
    public SaleHistoryDetail Detail { get; }

    public string SaleIdText { get; }

    public string StatusText { get; }

    public string CompletedAtLocalText { get; }

    public string CashierDisplayName => Detail.CashierDisplayName;

    public string RegisterName => Detail.RegisterName;

    public string SubtotalText { get; }

    public string TotalText { get; }

    public IReadOnlyList<SaleHistoryDetailLineRowViewModel> Lines { get; }

    public IReadOnlyList<SaleHistoryDetailPaymentRowViewModel> Payments { get; }

    public SaleHistoryDetailViewModel(SaleHistoryDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);

        Detail = detail;
        SaleIdText = detail.SaleId.ToString();
        StatusText = SalesHistoryDisplayFormatter.ToStatusLabel(detail.Status);
        CompletedAtLocalText = detail.CompletedAtUtc is { } completedAtUtc
            ? completedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture)
            : "-";
        SubtotalText = $"{detail.Subtotal.ToString("N2", CultureInfo.CurrentCulture)} {detail.Currency}";
        TotalText = $"{detail.Total.ToString("N2", CultureInfo.CurrentCulture)} {detail.Currency}";
        Lines = detail.Lines.Select(line => new SaleHistoryDetailLineRowViewModel(line)).ToList();
        Payments = detail.Payments.Select(payment => new SaleHistoryDetailPaymentRowViewModel(payment)).ToList();
    }
}

public sealed class SaleHistoryDetailLineRowViewModel
{
    public SaleHistoryDetailLine Line { get; }

    public string ProductSku => Line.ProductSku;

    public string ProductName => Line.ProductName;

    public string QuantityText { get; }

    public string UnitPriceText { get; }

    public string LineTotalText { get; }

    public SaleHistoryDetailLineRowViewModel(SaleHistoryDetailLine line)
    {
        ArgumentNullException.ThrowIfNull(line);

        Line = line;
        QuantityText = line.Quantity.ToString("0.##", CultureInfo.CurrentCulture);
        UnitPriceText = $"{line.UnitPrice.ToString("N2", CultureInfo.CurrentCulture)} {line.Currency}";
        LineTotalText = $"{line.LineTotal.ToString("N2", CultureInfo.CurrentCulture)} {line.Currency}";
    }
}

public sealed class SaleHistoryDetailPaymentRowViewModel
{
    public SaleHistoryDetailPayment Payment { get; }

    public string MethodText { get; }

    public string AmountText { get; }

    public string PaidAtLocalText { get; }

    // Solo tiene contenido para Manual Card (TAREA 25C): la referencia/autorización de la terminal
    // externa. Cash siempre la deja vacía (Payment.Reference es null para Cash).
    public string ReferenceText { get; }

    public SaleHistoryDetailPaymentRowViewModel(SaleHistoryDetailPayment payment)
    {
        ArgumentNullException.ThrowIfNull(payment);

        Payment = payment;
        MethodText = SalesHistoryDisplayFormatter.ToMethodLabel(payment.Method);
        AmountText = $"{payment.Amount.ToString("N2", CultureInfo.CurrentCulture)} {payment.Currency}";
        PaidAtLocalText = payment.PaidAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);
        ReferenceText = payment.Reference is { } reference ? $"Ref: {reference}" : string.Empty;
    }
}
