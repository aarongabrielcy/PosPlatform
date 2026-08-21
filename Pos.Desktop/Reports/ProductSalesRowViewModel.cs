using System.Globalization;
using Pos.Application.Reports;

namespace Pos.Desktop.Reports;

// Fila de "Ventas por producto" (sección 14 de la tarea): Sku/ProductName provienen del snapshot
// histórico, nunca del catálogo actual (ver ProductSalesReportItem).
public sealed class ProductSalesRowViewModel
{
    public ProductSalesReportItem Item { get; }

    public string Sku => Item.Sku;

    public string ProductName => Item.ProductName;

    public string QuantityText { get; }

    public string AmountText { get; }

    public ProductSalesRowViewModel(ProductSalesReportItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        Item = item;
        QuantityText = item.QuantitySold.ToString("0.##", CultureInfo.CurrentCulture);
        AmountText = $"{item.SalesAmount.ToString("N2", CultureInfo.CurrentCulture)} {item.Currency}";
    }
}
