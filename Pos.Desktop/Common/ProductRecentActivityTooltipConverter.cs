using System.Globalization;
using System.Text;
using System.Windows.Data;
using Pos.Application.ProductAudit;

namespace Pos.Desktop.Common;

// Resumen corto mostrado al hover sobre el indicador de actividad reciente (TAREA 24D, sección
// 31): máximo Action + actor + fecha relativa + 1-2 cambios, nunca 20 campos.
public sealed class ProductRecentActivityTooltipConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ProductRecentActivity activity)
        {
            return null;
        }

        var builder = new StringBuilder();
        builder.AppendLine(ProductAuditDisplayFormatter.ToActionLabel(activity.Action));

        foreach (var change in activity.TopChanges)
        {
            var fieldLabel = ProductAuditDisplayFormatter.ToFieldLabel(change.FieldName);
            var oldValue = ProductAuditDisplayFormatter.ToValueLabel(change.FieldName, change.OldValue);
            var newValue = ProductAuditDisplayFormatter.ToValueLabel(change.FieldName, change.NewValue);
            builder.AppendLine(CultureInfo.CurrentCulture, $"{fieldLabel}: {oldValue} → {newValue}");
        }

        var remaining = activity.TotalChangesCount - activity.TopChanges.Count;

        if (remaining > 0)
        {
            builder.AppendLine(CultureInfo.CurrentCulture, $"+ {remaining} cambios");
        }

        builder.AppendLine(CultureInfo.CurrentCulture, $"Por: {activity.ActorDisplayName}");
        builder.Append(ProductAuditDisplayFormatter.ToRelativeTime(activity.OccurredAtUtc, DateTimeOffset.UtcNow));

        return builder.ToString();
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
