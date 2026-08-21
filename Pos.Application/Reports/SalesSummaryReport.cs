namespace Pos.Application.Reports;

// Resumen operativo de ventas para un período (BASIC-RPT-01, sección 9): GrossSales/CashSales/
// CardSales se derivan exclusivamente de Sale/SaleLine/Payment de ventas Completed, nunca de
// CashMovement (CashIn/CashOut nunca entran aquí - sección 25 de la tarea). Igual invariante que
// RegisterClosingSummary: GrossSales nunca se deriva sumando CashSales + CardSales (puede haber
// otros métodos de pago, p. ej. BankTransfer, sin desglose propio en este reporte).
public sealed class SalesSummaryReport
{
    public int SaleCount { get; }

    public decimal GrossSales { get; }

    public decimal CashSales { get; }

    public decimal CardSales { get; }

    public string Currency { get; }

    public SalesSummaryReport(int saleCount, decimal grossSales, decimal cashSales, decimal cardSales, string currency)
    {
        ArgumentNullException.ThrowIfNull(currency);

        SaleCount = saleCount;
        GrossSales = grossSales;
        CashSales = cashSales;
        CardSales = cardSales;
        Currency = currency;
    }

    public static SalesSummaryReport Empty { get; } = new(0, 0m, 0m, 0m, string.Empty);
}
