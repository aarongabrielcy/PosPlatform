namespace Pos.Application.Receipts;

public interface IReceiptPrintingService
{
    // Llamado por la orquestación de Desktop INMEDIATAMENTE después de que ICheckoutService haya
    // confirmado el commit financiero (sección 3/21/22 de la tarea): nunca antes, nunca dentro de la
    // misma transacción. cashTendered/changeDue solo deben pasarse cuando el método de pago fue
    // Cash (provienen de CheckoutSummary, el único punto donde existen: Payment no los persiste).
    Task<ReceiptPrintResult> PrintAfterSaleAsync(
        Guid saleId, decimal? cashTendered, decimal? changeDue, CancellationToken cancellationToken = default);

    // Reimpreso manual desde Historial de ventas > Detalle de venta (sección 23-26): exige el
    // permiso ReprintReceipt a nivel de aplicación (no solo visibilidad de botón) y nunca muta
    // Sale/Payment/Inventory/RegisterSession/CashMovement.
    Task<ReceiptPrintResult> ReprintAsync(Guid saleId, CancellationToken cancellationToken = default);
}
