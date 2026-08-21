namespace Pos.Application.RegisterSessions;

// Vista previa del cierre de caja (TAREA 25A-FIX sección 5): se calcula bajo demanda a partir de
// ISaleRepository, nunca se persiste. CloseAsync vuelve a calcular el mismo valor de forma
// independiente al confirmar el cierre: Desktop nunca puede dictar ExpectedCash.
// TAREA 25C: CompletedCardSales se agrega solo para desglose informativo — un pago Card jamás
// forma parte de ExpectedCash (no es efectivo físico en el cajón).
// TAREA 25C-FIX sección 8-10: GrossSales NUNCA se deriva sumando CompletedCashSales +
// CompletedCardSales. Se calcula independientemente desde ISaleRepository.
// GetCompletedGrossTotalByRegisterSessionAsync (suma de Sale.Total vía SaleLine): Sale ya admite
// varios Payment por venta y el dominio ya define métodos además de Cash/Card (p. ej.
// BankTransfer), así que derivarlo del desglose por método subcontaría o duplicaría según cómo
// evolucione el pago de una venta.
// BASIC-CASH-01 (sección 10-11): CashIn/CashOut son movimientos manuales de efectivo, nunca ventas
// - no afectan GrossSales/CompletedCashSales/CompletedCardSales. ExpectedCash ya incluye
// + CashIn - CashOut (ver RegisterSessionService.GetClosingSummaryAsync).
public sealed class RegisterClosingSummary
{
    public decimal OpeningFloat { get; }

    public decimal CompletedCashSales { get; }

    public decimal CompletedCardSales { get; }

    public decimal GrossSales { get; }

    public decimal CashIn { get; }

    public decimal CashOut { get; }

    public decimal ExpectedCash { get; }

    public string Currency { get; }

    public RegisterClosingSummary(
        decimal openingFloat,
        decimal completedCashSales,
        decimal completedCardSales,
        decimal grossSales,
        decimal cashIn,
        decimal cashOut,
        decimal expectedCash,
        string currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        OpeningFloat = openingFloat;
        CompletedCashSales = completedCashSales;
        CompletedCardSales = completedCardSales;
        GrossSales = grossSales;
        CashIn = cashIn;
        CashOut = cashOut;
        ExpectedCash = expectedCash;
        Currency = currency;
    }
}
