namespace Pos.Application.Reports;

// Totales del período (sección 13 de la tarea) calculados sobre EXACTAMENTE el mismo filtro que
// Entries, nunca sobre una página cargada: igual criterio que SalesHistorySummary.
public sealed class CashMovementsReportResult
{
    public IReadOnlyList<CashMovementReportEntry> Entries { get; }

    public decimal CashInTotal { get; }

    public decimal CashOutTotal { get; }

    public string Currency { get; }

    public CashMovementsReportResult(
        IReadOnlyList<CashMovementReportEntry> entries, decimal cashInTotal, decimal cashOutTotal, string currency)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(currency);

        Entries = entries;
        CashInTotal = cashInTotal;
        CashOutTotal = cashOutTotal;
        Currency = currency;
    }

    public static CashMovementsReportResult Empty { get; } =
        new(Array.Empty<CashMovementReportEntry>(), 0m, 0m, string.Empty);
}
