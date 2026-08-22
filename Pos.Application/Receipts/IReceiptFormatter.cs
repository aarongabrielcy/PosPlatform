namespace Pos.Application.Receipts;

// Puerto de renderizado (sección 11 de la tarea): traduce el modelo Receipt a un payload de
// impresora. La implementación concreta (ESC/POS) vive en Pos.Hardware; Pos.Application y las
// ViewModels de WPF nunca generan comandos de impresora directamente (sección 12).
public interface IReceiptFormatter
{
    FormattedReceipt Format(Receipt receipt);
}
