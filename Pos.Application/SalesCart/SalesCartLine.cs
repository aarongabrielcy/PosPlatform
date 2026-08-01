using Pos.Domain.Common.Identifiers;

namespace Pos.Application.SalesCart;

// Línea del carrito en memoria: no es una entidad Domain (no es SaleLine) y nunca se persiste.
// Solo expone tipos primitivos (decimal/string) además de ProductId: igual que
// ActiveRegisterSession, evita filtrar Money/Sku hacia Pos.Desktop, que no debe depender de
// Pos.Domain. LineTaxAmount siempre es cero porque Product/Sale/SaleLine no representan
// impuestos hoy; si el modelo llegara a incorporarlos, este es el único punto que debe cambiar.
public sealed class SalesCartLine
{
    public ProductId ProductId { get; }

    public string Sku { get; }

    public string ProductName { get; }

    public decimal Quantity { get; }

    public decimal UnitPriceAmount { get; }

    public string Currency { get; }

    // Ya calculado en Money por SalesCartService (unitPrice * quantity) antes de llegar aquí:
    // conserva las reglas de redondeo de Domain sin que este tipo dependa de Money.
    public decimal LineSubtotalAmount { get; }

    public decimal LineTaxAmount { get; }

    public decimal LineTotalAmount { get; }

    // Existencia consultada al momento de agregar/actualizar la línea. Es informativa para la UI,
    // no una reserva: se vuelve a consultar en cada operación posterior.
    public decimal AvailableQuantity { get; }

    public bool TracksInventory { get; }

    public SalesCartLine(
        ProductId productId,
        string sku,
        string productName,
        decimal quantity,
        decimal unitPriceAmount,
        decimal lineSubtotalAmount,
        string currency,
        decimal availableQuantity,
        bool tracksInventory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentException.ThrowIfNullOrWhiteSpace(productName);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        if (quantity <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity debe ser mayor que cero.");
        }

        ProductId = productId;
        Sku = sku;
        ProductName = productName;
        Quantity = quantity;
        UnitPriceAmount = unitPriceAmount;
        Currency = currency;
        LineSubtotalAmount = lineSubtotalAmount;
        LineTaxAmount = 0m;
        LineTotalAmount = lineSubtotalAmount;
        AvailableQuantity = availableQuantity;
        TracksInventory = tracksInventory;
    }
}
