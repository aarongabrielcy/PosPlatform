using Pos.Domain.Common.Identifiers;

namespace Pos.Application.Reports;

// Fila de "Cierres de caja" (RPT-CASH-01) y también su propio detalle (sección 11/12 de la tarea):
// ambos comparten exactamente los mismos campos, la única diferencia es la presentación en
// Desktop. CashSales/CardSales/GrossSales/CashIn/CashOut se derivan de Sale/Payment/CashMovement
// por RegisterSessionId, nunca se persisten por separado (sección 19: no crear estado contable
// duplicado). ExpectedCash/CountedCash/Difference SÍ provienen de RegisterSession, que ya los
// persiste al cerrar (Domain.RegisterSessions.RegisterSession.Close).
public sealed class RegisterClosureReportItem
{
    public RegisterSessionId RegisterSessionId { get; }

    public string RegisterName { get; }

    public DateTimeOffset OpenedAtUtc { get; }

    public DateTimeOffset ClosedAtUtc { get; }

    public UserId OpenedByUserId { get; }

    public string OpenedByDisplayName { get; }

    public UserId ClosedByUserId { get; }

    public string ClosedByDisplayName { get; }

    public decimal OpeningFloat { get; }

    public decimal CashSales { get; }

    public decimal CardSales { get; }

    public decimal GrossSales { get; }

    public decimal CashIn { get; }

    public decimal CashOut { get; }

    public decimal ExpectedCash { get; }

    public decimal CountedCash { get; }

    public decimal Difference { get; }

    public string Currency { get; }

    public RegisterClosureReportItem(
        RegisterSessionId registerSessionId,
        string registerName,
        DateTimeOffset openedAtUtc,
        DateTimeOffset closedAtUtc,
        UserId openedByUserId,
        string openedByDisplayName,
        UserId closedByUserId,
        string closedByDisplayName,
        decimal openingFloat,
        decimal cashSales,
        decimal cardSales,
        decimal grossSales,
        decimal cashIn,
        decimal cashOut,
        decimal expectedCash,
        decimal countedCash,
        decimal difference,
        string currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(registerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(openedByDisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(closedByDisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        RegisterSessionId = registerSessionId;
        RegisterName = registerName;
        OpenedAtUtc = openedAtUtc;
        ClosedAtUtc = closedAtUtc;
        OpenedByUserId = openedByUserId;
        OpenedByDisplayName = openedByDisplayName;
        ClosedByUserId = closedByUserId;
        ClosedByDisplayName = closedByDisplayName;
        OpeningFloat = openingFloat;
        CashSales = cashSales;
        CardSales = cardSales;
        GrossSales = grossSales;
        CashIn = cashIn;
        CashOut = cashOut;
        ExpectedCash = expectedCash;
        CountedCash = countedCash;
        Difference = difference;
        Currency = currency;
    }
}
