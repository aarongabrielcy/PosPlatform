using Pos.Domain.Common.Identifiers;
using Pos.Domain.Inventory;

namespace Pos.Application.Inventory;

// Ninguno de los campos es obligatorio (TAREA 24G, sección 26), igual criterio que
// ProductAuditFilter. ProductId permite el filtro exacto de "Ver movimientos" desde una fila de
// Existencias; SearchTerm es la búsqueda libre por SKU/nombre del historial global.
public sealed record InventoryMovementFilter(
    ProductId? ProductId = null,
    string? SearchTerm = null,
    InventoryMovementType? Type = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null)
{
    public static InventoryMovementFilter Empty { get; } = new();
}
