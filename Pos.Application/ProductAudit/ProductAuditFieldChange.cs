using Pos.Domain.ProductAudit;

namespace Pos.Application.ProductAudit;

// Proyección de solo lectura de ProductAuditChange. No expone la entidad Domain ni Records de
// Infrastructure: solo primitivos + el enum ProductAuditField (igual que ProductCatalogItem no
// expone Money/Sku/Barcode, pero sí expone enums Domain como InventoryAdjustmentType).
public sealed class ProductAuditFieldChange
{
    public ProductAuditField FieldName { get; }

    public string? OldValue { get; }

    public string? NewValue { get; }

    public ProductAuditFieldChange(ProductAuditField fieldName, string? oldValue, string? newValue)
    {
        FieldName = fieldName;
        OldValue = oldValue;
        NewValue = newValue;
    }
}
