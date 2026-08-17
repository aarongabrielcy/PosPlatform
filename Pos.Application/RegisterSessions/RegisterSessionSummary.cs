namespace Pos.Application.RegisterSessions;

// Resumen inmutable devuelto tras cerrar una caja. TAREA 25C: CashSales/CardSales/GrossSales
// reflejan el mismo desglose que RegisterClosingSummary, recalculado en el momento del cierre
// (nunca lo dicta Desktop). ExpectedAmount ya excluye CardSales (RegisterSessionService.CloseAsync).
// TAREA 25C-FIX sección 8-10: GrossSales proviene de ISaleRepository.
// GetCompletedGrossTotalByRegisterSessionAsync (suma de Sale.Total), nunca de CashSales + CardSales.
public sealed class RegisterSessionSummary
{
    public string RegisterName { get; }

    public DateTimeOffset OpenedAtUtc { get; }

    public DateTimeOffset ClosedAtUtc { get; }

    public string OpenedByDisplayName { get; }

    public string ClosedByDisplayName { get; }

    public decimal OpeningAmount { get; }

    public decimal CashSales { get; }

    public decimal CardSales { get; }

    public decimal GrossSales { get; }

    public decimal ClosingAmount { get; }

    public decimal ExpectedAmount { get; }

    public decimal Difference { get; }

    public string Currency { get; }

    public RegisterSessionSummary(
        string registerName,
        DateTimeOffset openedAtUtc,
        DateTimeOffset closedAtUtc,
        string openedByDisplayName,
        string closedByDisplayName,
        decimal openingAmount,
        decimal cashSales,
        decimal cardSales,
        decimal grossSales,
        decimal closingAmount,
        decimal expectedAmount,
        decimal difference,
        string currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(registerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(openedByDisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(closedByDisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        RegisterName = registerName;
        OpenedAtUtc = openedAtUtc;
        ClosedAtUtc = closedAtUtc;
        OpenedByDisplayName = openedByDisplayName;
        ClosedByDisplayName = closedByDisplayName;
        OpeningAmount = openingAmount;
        CashSales = cashSales;
        CardSales = cardSales;
        GrossSales = grossSales;
        ClosingAmount = closingAmount;
        ExpectedAmount = expectedAmount;
        Difference = difference;
        Currency = currency;
    }
}
