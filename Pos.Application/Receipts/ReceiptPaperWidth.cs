namespace Pos.Application.Receipts;

// V1 soporta los dos anchos comerciales más comunes de impresora térmica (sección 17 de la tarea).
// El formateador ESC/POS (Pos.Hardware) traduce cada valor a un número de columnas de texto fijo.
public enum ReceiptPaperWidth
{
    Mm58,
    Mm80,
}
