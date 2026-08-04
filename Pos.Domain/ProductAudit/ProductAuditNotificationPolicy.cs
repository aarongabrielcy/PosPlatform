namespace Pos.Domain.ProductAudit;

// Política V1 de notificaciones administrativas (TAREA 24E, sección 3): decide si un
// ProductAuditEvent amerita una AdministrativeNotification. Vive en Domain porque solo depende de
// ProductAuditEvent/ProductAuditField, sin necesidad de nada de Application.
public static class ProductAuditNotificationPolicy
{
    private static readonly ProductAuditField[] SensitiveUpdatedFields =
    [
        ProductAuditField.Sku,
        ProductAuditField.SalePrice,
        ProductAuditField.Cost,
    ];

    public static bool ShouldNotify(ProductAuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        return auditEvent.Action switch
        {
            // Created nunca notifica (TAREA 24E, sección 12): el alta de un producto no es una
            // alerta administrativa.
            ProductAuditAction.Created => false,

            // Updated solo notifica si al menos un cambio toca un campo sensible (Sku/SalePrice/
            // Cost). Name/Description/Barcode/ReorderPoint por sí solos nunca notifican (sección
            // 13).
            ProductAuditAction.Updated => auditEvent.Changes.Any(
                change => SensitiveUpdatedFields.Contains(change.FieldName)),

            ProductAuditAction.Activated => true,
            ProductAuditAction.Deactivated => true,
            ProductAuditAction.InventoryAdjusted => true,

            _ => false,
        };
    }
}
