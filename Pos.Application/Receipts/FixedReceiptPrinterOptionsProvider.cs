namespace Pos.Application.Receipts;

// Implementación trivial de IReceiptPrinterOptionsProvider que nunca cambia tras construirse: es la
// forma más simple de satisfacer el puerto cuando no existe (o no hace falta) una fuente de
// configuración local en tiempo de ejecución - típicamente pruebas automatizadas, que ya construían
// ReceiptPrintingService/WindowsSpoolReceiptPrinter/EscPosReceiptFormatter con un ReceiptPrinterOptions
// fijo antes de BASIC-CFG-01. RefreshAsync es un no-op deliberado: no hay ninguna fuente que releer.
public sealed class FixedReceiptPrinterOptionsProvider : IReceiptPrinterOptionsProvider
{
    public FixedReceiptPrinterOptionsProvider(ReceiptPrinterOptions options)
    {
        Current = options ?? throw new ArgumentNullException(nameof(options));
    }

    public ReceiptPrinterOptions Current { get; }

    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
