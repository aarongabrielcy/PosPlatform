using Pos.Domain.Common.Identifiers;

namespace Pos.Application.Reports;

// Fila del reporte "Actividad por operador" (sección 17 de la tarea): auditoría operativa simple,
// nunca ranking/comisión/HR. Se agrupa por Sale.CreatedByUserId (identidad estable); DisplayName
// se resuelve en vivo contra Users (igual criterio que SalesHistoryQuery/RegisterSessionService),
// así que un usuario renombrado o desactivado después del período sigue resolviendo su nombre
// actual sin romper el reporte.
public sealed class OperatorActivityReportItem
{
    public UserId UserId { get; }

    public string DisplayName { get; }

    public int CompletedSaleCount { get; }

    public decimal GrossSales { get; }

    public decimal CashSales { get; }

    public decimal CardSales { get; }

    public string Currency { get; }

    public OperatorActivityReportItem(
        UserId userId,
        string displayName,
        int completedSaleCount,
        decimal grossSales,
        decimal cashSales,
        decimal cardSales,
        string currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        UserId = userId;
        DisplayName = displayName;
        CompletedSaleCount = completedSaleCount;
        GrossSales = grossSales;
        CashSales = cashSales;
        CardSales = cardSales;
        Currency = currency;
    }
}
