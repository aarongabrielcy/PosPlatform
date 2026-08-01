using Pos.Domain.Common.Identifiers;

namespace Pos.Application.SalesCart;

// Proyección de Product+InventoryItem para la búsqueda del carrito. No expone la entidad Domain
// ni ProductRecord/InventoryItemRecord, ni tipos de Domain como Money/Sku: solo primitivos (igual
// que ActiveRegisterSession), para que Pos.Desktop nunca necesite depender de Pos.Domain.
public sealed class ProductSearchResult
{
    public ProductId ProductId { get; }

    public string Sku { get; }

    public string Name { get; }

    public decimal UnitPriceAmount { get; }

    public string Currency { get; }

    // Solo tiene significado cuando TracksInventory es true. Para productos sin control de
    // inventario permanece en cero: no se inventa una existencia infinita.
    public decimal AvailableQuantity { get; }

    public bool TracksInventory { get; }

    public bool IsAvailable { get; }

    public ProductSearchResult(
        ProductId productId,
        string sku,
        string name,
        decimal unitPriceAmount,
        string currency,
        decimal availableQuantity,
        bool tracksInventory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        ProductId = productId;
        Sku = sku;
        Name = name;
        UnitPriceAmount = unitPriceAmount;
        Currency = currency;
        AvailableQuantity = availableQuantity;
        TracksInventory = tracksInventory;
        IsAvailable = !tracksInventory || availableQuantity > 0m;
    }
}
