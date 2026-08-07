using System.Globalization;
using Pos.Application.Inventory;
using Pos.Domain.Inventory;

namespace Pos.Desktop.Inventory;

// Fila de la tabla "Movimientos" (TAREA 24G, sección 23): traduce el Type persistido (nunca se
// cambia el valor guardado, solo el texto mostrado) y muestra únicamente campos que
// InventoryMovement realmente persiste (QuantityBefore/QuantityAfter, no reconstruidos).
public sealed class InventoryMovementRowViewModel
{
    public InventoryMovementItem Item { get; }

    public string OccurredAtLocalText { get; }

    public string TypeText { get; }

    public string ChangeText { get; }

    public string ReferenceText { get; }

    public InventoryMovementRowViewModel(InventoryMovementItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        Item = item;
        OccurredAtLocalText = item.OccurredAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);

        TypeText = item.Type switch
        {
            InventoryMovementType.SaleDecrease => "Venta",
            InventoryMovementType.ManualIncrease => "Ajuste manual",
            InventoryMovementType.ManualDecrease => "Ajuste manual",
            _ => "Movimiento",
        };

        var isIncrease = item.QuantityAfter > item.QuantityBefore;
        ChangeText = isIncrease
            ? $"+{item.Quantity.ToString(CultureInfo.CurrentCulture)}"
            : $"-{item.Quantity.ToString(CultureInfo.CurrentCulture)}";

        ReferenceText = item.SaleId is { } saleId ? $"Venta {saleId}" : "—";
    }
}
