using System.Linq;
using Pos.Application.ProductAudit;
using Pos.Domain.ProductAudit;

namespace Pos.Desktop.Common;

// Genera título/resumen de una AdministrativeNotification en Desktop (TAREA 24E, sección 27/28):
// nunca se persiste este texto, se deriva del AuditAction/Changes ya cargados desde
// IProductAuditQuery, igual criterio que ProductAuditDisplayFormatter con la auditoría.
internal static class AdministrativeNotificationDisplayFormatter
{
    private static readonly ProductAuditField[] SensitiveUpdatedFields =
    [
        ProductAuditField.Sku,
        ProductAuditField.SalePrice,
        ProductAuditField.Cost,
    ];

    public static string ToTitle(ProductAuditAction action, IReadOnlyList<ProductAuditFieldChange> changes) =>
        action switch
        {
            ProductAuditAction.Activated => "Producto activado",
            ProductAuditAction.Deactivated => "Producto desactivado",
            ProductAuditAction.InventoryAdjusted => "Inventario ajustado",
            ProductAuditAction.Updated => ToUpdatedTitle(changes),
            _ => "Nueva actividad administrativa",
        };

    private static string ToUpdatedTitle(IReadOnlyList<ProductAuditFieldChange> changes)
    {
        var sensitiveFields = changes
            .Select(change => change.FieldName)
            .Where(field => SensitiveUpdatedFields.Contains(field))
            .Distinct()
            .ToList();

        if (sensitiveFields.Count != 1)
        {
            return "Producto modificado";
        }

        return sensitiveFields[0] switch
        {
            ProductAuditField.Sku => "SKU modificado",
            ProductAuditField.SalePrice => "Precio modificado",
            ProductAuditField.Cost => "Costo modificado",
            _ => "Producto modificado",
        };
    }

    // Máximo 1-2 changes relevantes (TAREA 24E, sección 28): nunca convierte el panel en una
    // segunda Auditoría.
    public static string ToSummary(IReadOnlyList<ProductAuditFieldChange> changes)
    {
        if (changes.Count == 0)
        {
            return string.Empty;
        }

        var shown = changes.Take(2).Select(change =>
            $"{ProductAuditDisplayFormatter.ToFieldLabel(change.FieldName)}: " +
            $"{ProductAuditDisplayFormatter.ToValueLabel(change.FieldName, change.OldValue)} → " +
            $"{ProductAuditDisplayFormatter.ToValueLabel(change.FieldName, change.NewValue)}");

        var text = string.Join(" · ", shown);
        var remaining = changes.Count - Math.Min(2, changes.Count);

        return remaining > 0 ? $"{text} (+{remaining} cambios)" : text;
    }
}
