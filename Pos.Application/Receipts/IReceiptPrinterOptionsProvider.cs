namespace Pos.Application.Receipts;

// BASIC-CFG-01, sección 32/33/35: reemplaza la inyección directa de ReceiptPrinterOptions como
// instancia singleton fija (BASIC-PRN-01) por una indirección mínima que permite que Configuración
// > Impresora tenga efecto inmediato sin reiniciar la aplicación. ReceiptPrintingService/
// WindowsSpoolReceiptPrinter/EscPosReceiptFormatter leen Current en cada operación (nunca lo
// cachean en un campo capturado en el constructor), así que un RefreshAsync posterior a un Guardar
// exitoso queda reflejado en la siguiente impresión sin reiniciar. La composición concreta
// (appsettings + appsettings.Local.json como base, LocalAppData como override - sección 35 de la
// tarea) vive en Pos.Desktop (único proyecto con IConfiguration disponible).
public interface IReceiptPrinterOptionsProvider
{
    ReceiptPrinterOptions Current { get; }

    Task RefreshAsync(CancellationToken cancellationToken = default);
}
