namespace Pos.Application.Receipts;

// Payload listo para enviar al transporte físico (spooler de Windows en modo RAW). Producido por
// IReceiptFormatter, consumido por IReceiptPrinter: ninguno de los dos tipos expone comandos
// ESC/POS ni bytes crudos fuera de este contenedor.
public sealed class FormattedReceipt
{
    public IReadOnlyList<byte> Payload { get; }

    public FormattedReceipt(IReadOnlyList<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        Payload = payload;
    }
}
