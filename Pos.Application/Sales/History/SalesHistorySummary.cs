namespace Pos.Application.Sales.History;

// Resumen del FILTRO COMPLETO, no de la página cargada en Desktop (TAREA 25B, sección 16/17):
// se calcula con una consulta agregada separada que respeta los mismos filtros que el listado.
// No mezcla OpeningFloat de RegisterSession: Total es exclusivamente la suma de Sale.Total de las
// ventas Completed que matchean el filtro.
public sealed class SalesHistorySummary
{
    public int SalesCount { get; }

    public decimal Total { get; }

    public string Currency { get; }

    public IReadOnlyList<SalesHistoryPaymentAmount> PaymentBreakdown { get; }

    public SalesHistorySummary(
        int salesCount, decimal total, string currency, IReadOnlyList<SalesHistoryPaymentAmount> paymentBreakdown)
    {
        ArgumentNullException.ThrowIfNull(currency);
        ArgumentNullException.ThrowIfNull(paymentBreakdown);

        SalesCount = salesCount;
        Total = total;
        Currency = currency;
        PaymentBreakdown = paymentBreakdown;
    }

    public static SalesHistorySummary Empty { get; } =
        new(0, 0m, string.Empty, Array.Empty<SalesHistoryPaymentAmount>());
}
