using Pos.Application.ProductAudit;
using Pos.Desktop.Common;

namespace Pos.Desktop.Audit.Products;

// Fila de la tabla "Campo / Antes / Después" del panel de detalle (TAREA 24D, sección 26).
public sealed class ProductAuditChangeDisplay
{
    public string FieldLabel { get; }

    public string OldValueDisplay { get; }

    public string NewValueDisplay { get; }

    public ProductAuditChangeDisplay(ProductAuditFieldChange change)
    {
        ArgumentNullException.ThrowIfNull(change);

        FieldLabel = ProductAuditDisplayFormatter.ToFieldLabel(change.FieldName);
        OldValueDisplay = ProductAuditDisplayFormatter.ToValueLabel(change.FieldName, change.OldValue);
        NewValueDisplay = ProductAuditDisplayFormatter.ToValueLabel(change.FieldName, change.NewValue);
    }
}
