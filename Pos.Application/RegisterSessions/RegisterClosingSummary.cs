namespace Pos.Application.RegisterSessions;

// Vista previa del cierre de caja (TAREA 25A-FIX sección 5): se calcula bajo demanda a partir de
// ISaleRepository, nunca se persiste. CloseAsync vuelve a calcular el mismo valor de forma
// independiente al confirmar el cierre: Desktop nunca puede dictar ExpectedCash.
public sealed class RegisterClosingSummary
{
    public decimal OpeningFloat { get; }

    public decimal CompletedCashSales { get; }

    public decimal ExpectedCash { get; }

    public string Currency { get; }

    public RegisterClosingSummary(
        decimal openingFloat, decimal completedCashSales, decimal expectedCash, string currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        OpeningFloat = openingFloat;
        CompletedCashSales = completedCashSales;
        ExpectedCash = expectedCash;
        Currency = currency;
    }
}
