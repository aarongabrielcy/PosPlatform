using System.Globalization;
using Pos.Application.Reports;

namespace Pos.Desktop.Reports;

// Fila de "Actividad por operador" (sección 17 de la tarea): auditoría operativa simple, nunca
// ranking/comisión.
public sealed class OperatorActivityRowViewModel
{
    public OperatorActivityReportItem Item { get; }

    public string DisplayName => Item.DisplayName;

    public int CompletedSaleCount => Item.CompletedSaleCount;

    public string GrossSalesText { get; }

    public string CashSalesText { get; }

    public string CardSalesText { get; }

    public OperatorActivityRowViewModel(OperatorActivityReportItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        Item = item;
        GrossSalesText = $"{item.GrossSales.ToString("N2", CultureInfo.CurrentCulture)} {item.Currency}";
        CashSalesText = $"{item.CashSales.ToString("N2", CultureInfo.CurrentCulture)} {item.Currency}";
        CardSalesText = $"{item.CardSales.ToString("N2", CultureInfo.CurrentCulture)} {item.Currency}";
    }
}
