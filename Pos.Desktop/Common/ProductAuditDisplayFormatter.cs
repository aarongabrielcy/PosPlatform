using System.Globalization;
using Pos.Domain.ProductAudit;

namespace Pos.Desktop.Common;

// Traducción de valores de auditoría a texto legible (TAREA 24D, sección 28): nunca se persiste
// esta traducción, se calcula solo en la UI a partir de Action/FieldName y de los valores
// determinísticos ya formateados por Pos.Application (ProductAuditValueFormatter).
internal static class ProductAuditDisplayFormatter
{
    public static string ToActionLabel(ProductAuditAction action) => action switch
    {
        ProductAuditAction.Created => "Creado",
        ProductAuditAction.Updated => "Actualizado",
        ProductAuditAction.Activated => "Activado",
        ProductAuditAction.Deactivated => "Desactivado",
        ProductAuditAction.InventoryAdjusted => "Inventario ajustado",
        _ => action.ToString(),
    };

    public static string ToFieldLabel(ProductAuditField field) => field switch
    {
        ProductAuditField.Sku => "SKU",
        ProductAuditField.Barcode => "Código de barras",
        ProductAuditField.Name => "Nombre",
        ProductAuditField.Description => "Descripción",
        ProductAuditField.SalePrice => "Precio de venta",
        ProductAuditField.Cost => "Costo",
        ProductAuditField.TracksInventory => "Controla inventario",
        ProductAuditField.ReorderPoint => "Stock mínimo",
        ProductAuditField.IsActive => "Estado",
        ProductAuditField.InventoryQuantity => "Existencia",
        _ => field.ToString(),
    };

    // IsActive/TracksInventory son los únicos campos booleanos; el resto ya llega formateado de
    // forma determinística desde Application (decimales/Money como texto). null siempre se
    // muestra como "—" (TAREA 24D, sección 28).
    public static string ToValueLabel(ProductAuditField field, string? rawValue)
    {
        if (rawValue is null)
        {
            return "—";
        }

        return field switch
        {
            ProductAuditField.IsActive => rawValue == "true" ? "Activo" : "Inactivo",
            ProductAuditField.TracksInventory => rawValue == "true" ? "Sí" : "No",
            _ => rawValue,
        };
    }

    // "Reciente" V1 usa esta misma escala para el resumen corto en Productos (TAREA 24D, sección 31).
    public static string ToRelativeTime(DateTimeOffset occurredAtUtc, DateTimeOffset nowUtc)
    {
        var elapsed = nowUtc - occurredAtUtc;

        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        if (elapsed.TotalMinutes < 1)
        {
            return "Hace un momento";
        }

        if (elapsed.TotalMinutes < 60)
        {
            return $"Hace {(int)elapsed.TotalMinutes} min";
        }

        if (elapsed.TotalHours < 24)
        {
            return $"Hace {(int)elapsed.TotalHours} h";
        }

        return occurredAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);
    }
}
