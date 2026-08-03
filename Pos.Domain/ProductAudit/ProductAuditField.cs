namespace Pos.Domain.ProductAudit;

// Campos auditables controlados (TAREA 24D, sección 6): FieldName nunca es un string arbitrario.
public enum ProductAuditField
{
    Sku,
    Barcode,
    Name,
    Description,
    SalePrice,
    Cost,
    TracksInventory,
    ReorderPoint,
    IsActive,
    InventoryQuantity,
}
