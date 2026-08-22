namespace Pos.Application.Receipts;

// Resultado tipado de un intento de impresión física (sección 13 de la tarea). TechnicalDetail es
// exclusivamente para diagnóstico interno (log del llamador, ver Pos.Desktop): nunca debe mostrarse
// al cajero (sección 29 — nada de stack traces ni códigos Win32 crudos en la UI).
public sealed class PrinterOutcome
{
    public PrinterOutcomeStatus Status { get; }

    public string? TechnicalDetail { get; }

    private PrinterOutcome(PrinterOutcomeStatus status, string? technicalDetail)
    {
        Status = status;
        TechnicalDetail = technicalDetail;
    }

    public static PrinterOutcome Success() => new(PrinterOutcomeStatus.Success, null);

    public static PrinterOutcome NotConfigured() => new(PrinterOutcomeStatus.NotConfigured, null);

    public static PrinterOutcome Unavailable(string? technicalDetail) =>
        new(PrinterOutcomeStatus.PrinterUnavailable, technicalDetail);

    public static PrinterOutcome Failed(string? technicalDetail) =>
        new(PrinterOutcomeStatus.PrintFailed, technicalDetail);
}
