namespace Pos.Application.Receipts;

public enum ReceiptPrintResultStatus
{
    Success,

    // Impresora deshabilitada/no configurada, o AutoPrint desactivado (solo aplica a la impresión
    // automática tras el cobro): no es un error, no debe mostrarse como advertencia (sección 19/21).
    Skipped,

    // Solo puede ocurrir en ReprintAsync (sección 25/26): el usuario autenticado no tiene el permiso
    // ReprintReceipt.
    NotAuthorized,

    SaleNotFound,

    PrinterUnavailable,

    PrintFailed,
}
