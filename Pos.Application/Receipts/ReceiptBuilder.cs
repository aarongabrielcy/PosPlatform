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

    // BASIC-CFG-01, sección 15: contenido de "Imprimir prueba" desde Configuración > Impresora.
    // Nunca proviene de un SaleHistoryDetail real (no existe Sale): es un Receipt inofensivo
    // construido a mano que ejercita el mismo IReceiptFormatter/IReceiptPrinter que una venta real,
    // incluyendo el ancho de papel configurado y caracteres acentuados en español (á é í ó ú ñ Ñ),
    // para que la prueba sea representativa del ticket real sin imprimir datos comerciales.
    public static Receipt BuildTestReceipt(ReceiptPaperWidth paperWidth, DateTimeOffset nowUtc)
    {
        var widthLabel = paperWidth == ReceiptPaperWidth.Mm58 ? "58 mm" : "80 mm";

        var lines = new List<ReceiptLine>
        {
            new("TEST", $"Prueba de impresión ({widthLabel})", 1m, 0m, 0m),
            new("TEST-ES", "á é í ó ú ñ Ñ", 1m, 0m, 0m),
        };

        return new Receipt(
            "PRUEBA",
            nowUtc,
            "POSPlatform",
            "-",
            "Sistema",
            lines,
            subtotal: 0m,
            total: 0m,
            currency: "-",
            payments: [],
            cashTendered: null,
            changeDue: null,
            isReprint: false);
    }
}
