namespace Pos.Application.Receipts;

// Puerto de transporte físico (sección 13 de la tarea). Implementado en Pos.Hardware (spooler de
// Windows en modo RAW). Nunca lanza para fallas de hardware esperables (impresora apagada, sin
// papel, no instalada): esos casos se comunican mediante PrinterOutcome, no excepciones.
public interface IReceiptPrinter
{
    Task<PrinterOutcome> PrintAsync(FormattedReceipt receipt, CancellationToken cancellationToken = default);
}
