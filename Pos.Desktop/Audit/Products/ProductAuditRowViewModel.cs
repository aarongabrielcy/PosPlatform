using System.Globalization;
using System.Linq;
using Pos.Application.ProductAudit;
using Pos.Desktop.Common;

namespace Pos.Desktop.Audit.Products;

// Fila de la tabla Auditoría > Productos (TAREA 24D, sección 25): envuelve ProductAuditEntry con
// texto ya formateado para XAML, igual criterio que ProductCatalogItem se usa directamente en
// ProductsView pero aquí los campos de fecha/acción/resumen requieren cálculo previo.
public sealed class ProductAuditRowViewModel
{
    public ProductAuditEntry Entry { get; }

    public string OccurredAtLocalText { get; }

    public string ActorDisplayName => Entry.ActorDisplayName;

    public string ProductSku => Entry.ProductSku;

    public string ProductName => Entry.ProductName;

    public string ActionText { get; }

    public string SummaryText { get; }

    public IReadOnlyList<ProductAuditChangeDisplay> Changes { get; }

    public ProductAuditRowViewModel(ProductAuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        Entry = entry;
        OccurredAtLocalText = entry.OccurredAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);
        ActionText = ProductAuditDisplayFormatter.ToActionLabel(entry.Action);
        Changes = entry.Changes.Select(change => new ProductAuditChangeDisplay(change)).ToList();
        SummaryText = BuildSummary(Changes);
    }

    private static string BuildSummary(IReadOnlyList<ProductAuditChangeDisplay> changes)
    {
        if (changes.Count == 0)
        {
            return string.Empty;
        }

        var first = changes[0];
        var text = $"{first.FieldLabel}: {first.OldValueDisplay} → {first.NewValueDisplay}";

        return changes.Count > 1 ? $"{text} (+{changes.Count - 1} cambios)" : text;
    }
}
