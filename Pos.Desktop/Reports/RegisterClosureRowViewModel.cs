using System.Globalization;
using Pos.Application.Reports;

namespace Pos.Desktop.Reports;

// Fila de "Cierres de caja" (RPT-CASH-01, sección 11 de la tarea): envuelve RegisterClosureReportItem
// con texto ya formateado para XAML, igual criterio que InventoryCatalogRowViewModel.
public sealed class RegisterClosureRowViewModel
{
    public RegisterClosureReportItem Item { get; }

    public string RegisterName => Item.RegisterName;

    public string OpenedAtLocalText { get; }

    public string ClosedAtLocalText { get; }

    public string OpenedByDisplayName => Item.OpenedByDisplayName;

    public string ClosedByDisplayName => Item.ClosedByDisplayName;

    public string OpeningFloatText { get; }

    public string CashSalesText { get; }

    public string CardSalesText { get; }

    public string GrossSalesText { get; }

    public string CashInText { get; }

    public string CashOutText { get; }

    public string ExpectedCashText { get; }

    public string CountedCashText { get; }

    public string DifferenceText { get; }

    public RegisterClosureRowViewModel(RegisterClosureReportItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        Item = item;
        OpenedAtLocalText = item.OpenedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);
        ClosedAtLocalText = item.ClosedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);
        OpeningFloatText = FormatAmount(item.OpeningFloat, item.Currency);
        CashSalesText = FormatAmount(item.CashSales, item.Currency);
        CardSalesText = FormatAmount(item.CardSales, item.Currency);
        GrossSalesText = FormatAmount(item.GrossSales, item.Currency);
        CashInText = FormatAmount(item.CashIn, item.Currency);
        CashOutText = FormatAmount(item.CashOut, item.Currency);
        ExpectedCashText = FormatAmount(item.ExpectedCash, item.Currency);
        CountedCashText = FormatAmount(item.CountedCash, item.Currency);
        DifferenceText = FormatAmount(item.Difference, item.Currency);
    }

    private static string FormatAmount(decimal amount, string currency) =>
        $"{amount.ToString("N2", CultureInfo.CurrentCulture)} {currency}";
}

// Detalle de un cierre (sección 12 de la tarea): separa explícitamente la reconciliación de
// efectivo (OpeningFloat + CashSales + CashIn - CashOut = ExpectedCash, luego CountedCash y
// Difference) de las ventas (CashSales/CardSales/GrossSales), nunca mezclando balance de cajón con
// ingresos - mismo invariante que RegisterClosingSummary/RegisterSessionSummary en Application.
public sealed class RegisterClosureDetailViewModel
{
    public RegisterClosureReportItem Item { get; }

    public string RegisterName => Item.RegisterName;

    public string OpenedAtLocalText { get; }

    public string ClosedAtLocalText { get; }

    public string OpenedByDisplayName => Item.OpenedByDisplayName;

    public string ClosedByDisplayName => Item.ClosedByDisplayName;

    public string OpeningFloatText { get; }

    public string CashInText { get; }

    public string CashOutText { get; }

    public string ExpectedCashText { get; }

    public string CountedCashText { get; }

    public string DifferenceText { get; }

    public string CashSalesText { get; }

    public string CardSalesText { get; }

    public string GrossSalesText { get; }

    public RegisterClosureDetailViewModel(RegisterClosureReportItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        Item = item;
        OpenedAtLocalText = item.OpenedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);
        ClosedAtLocalText = item.ClosedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);
        OpeningFloatText = FormatAmount(item.OpeningFloat, item.Currency);
        CashInText = FormatAmount(item.CashIn, item.Currency);
        CashOutText = FormatAmount(item.CashOut, item.Currency);
        ExpectedCashText = FormatAmount(item.ExpectedCash, item.Currency);
        CountedCashText = FormatAmount(item.CountedCash, item.Currency);
        DifferenceText = FormatAmount(item.Difference, item.Currency);
        CashSalesText = FormatAmount(item.CashSales, item.Currency);
        CardSalesText = FormatAmount(item.CardSales, item.Currency);
        GrossSalesText = FormatAmount(item.GrossSales, item.Currency);
    }

    private static string FormatAmount(decimal amount, string currency) =>
        $"{amount.ToString("N2", CultureInfo.CurrentCulture)} {currency}";
}
