namespace Pos.Application.SalesCart;

// Proyección inmutable del carrito en curso, pensada para vivir dentro de ICurrentSalesCart.
// Nunca se persiste: Sale/SaleLine solo se crean en la futura fase de cobro. Solo expone
// decimal/string (igual que ActiveRegisterSession) para que Pos.Desktop nunca necesite depender
// de Pos.Domain al enlazar estos totales.
public sealed class SalesCartSnapshot
{
    public IReadOnlyList<SalesCartLine> Lines { get; }

    public decimal SubtotalAmount { get; }

    // El dominio no modela descuentos ni impuestos en Product/Sale/SaleLine: ambos totales
    // permanecen en cero hasta que ese modelo exista. No se inventa una regla de cálculo.
    public decimal DiscountTotalAmount { get; }

    public decimal TaxTotalAmount { get; }

    public decimal TotalAmount { get; }

    public string Currency { get; }

    public bool HasItems => Lines.Count > 0;

    public SalesCartSnapshot(IReadOnlyList<SalesCartLine> lines, string currency)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        Lines = lines;
        Currency = currency;

        // Cada LineSubtotalAmount ya viene redondeado a 2 decimales por Money en
        // SalesCartService: sumar valores decimal ya redondeados es exacto, sin necesidad de
        // Money aquí.
        var subtotal = 0m;

        foreach (var line in lines)
        {
            subtotal += line.LineSubtotalAmount;
        }

        SubtotalAmount = subtotal;
        DiscountTotalAmount = 0m;
        TaxTotalAmount = 0m;
        TotalAmount = SubtotalAmount;
    }

    public static SalesCartSnapshot Empty(string currency) => new(Array.Empty<SalesCartLine>(), currency);
}
