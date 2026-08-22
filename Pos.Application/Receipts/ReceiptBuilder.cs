using Pos.Application.Sales.History;

namespace Pos.Application.Receipts;

// Construye Receipt EXCLUSIVAMENTE a partir de SaleHistoryDetail (snapshot histórico ya
// persistido): ni la impresión inicial ni el reimpreso reconsultan Product/precio actual (sección
// 6/36 de la tarea). cashTendered/changeDue son la única entrada externa al snapshot porque Payment
// no los persiste; deben venir de CheckoutSummary únicamente en la impresión inicial y ser null en
// cualquier reimpreso.
public static class ReceiptBuilder
{
    public static Receipt Build(
        SaleHistoryDetail detail,
        string businessName,
        bool isReprint,
        decimal? cashTendered,
        decimal? changeDue)
    {
        ArgumentNullException.ThrowIfNull(detail);
        ArgumentException.ThrowIfNullOrWhiteSpace(businessName);

        var lines = detail.Lines
            .Select(line => new ReceiptLine(line.ProductSku, line.ProductName, line.Quantity, line.UnitPrice, line.LineTotal))
            .ToList();

        var payments = detail.Payments
            .Select(payment => new ReceiptPayment(payment.Method, payment.Amount, payment.Reference))
            .ToList();

        return new Receipt(
            ShortSaleReference(detail.SaleId.Value),
            detail.CompletedAtUtc ?? detail.CreatedAtUtc,
            businessName,
            detail.RegisterName,
            detail.CashierDisplayName,
            lines,
            detail.Subtotal,
            detail.Total,
            detail.Currency,
            payments,
            cashTendered,
            changeDue,
            isReprint);
    }

    // Mismo mecanismo que SalesHistoryRowViewModel.SaleIdShortText (Pos.Desktop): no existe un
    // segundo esquema de folio/ticket (sección 10 de la tarea), se reutiliza el fragmento corto ya
    // mostrado en Historial de ventas.
    private static string ShortSaleReference(Guid saleId) =>
        saleId.ToString("N").Substring(0, 8).ToUpperInvariant();
}
