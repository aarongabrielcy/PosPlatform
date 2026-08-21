using Pos.Domain.Common.Identifiers;

namespace Pos.Application.Reports;

// Fila del reporte "Ventas por producto" (sección 14/15 de la tarea): Sku/ProductName provienen
// del snapshot histórico de SaleLine (nunca de la fila Product actual), así que un producto
// renombrado o desactivado después de la venta no corrompe el reporte. Se agrupa por ProductId
// (identidad estable); si el producto cambió de nombre/SKU durante el período, se muestra el
// snapshot de la venta más reciente del grupo. Nunca expone costo/margen/utilidad (sección 14).
public sealed class ProductSalesReportItem
{
    public ProductId ProductId { get; }

    public string Sku { get; }

    public string ProductName { get; }

    public decimal QuantitySold { get; }

    public decimal SalesAmount { get; }

    public string Currency { get; }

    public ProductSalesReportItem(
        ProductId productId, string sku, string productName, decimal quantitySold, decimal salesAmount, string currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentException.ThrowIfNullOrWhiteSpace(productName);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        ProductId = productId;
        Sku = sku;
        ProductName = productName;
        QuantitySold = quantitySold;
        SalesAmount = salesAmount;
        Currency = currency;
    }
}
