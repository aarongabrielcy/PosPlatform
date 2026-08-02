using System.Globalization;
using System.Windows.Data;

namespace Pos.Desktop.Common;

// Traduce Product.IsActive a "Activo"/"Inactivo" para la columna Estado del catálogo.
public sealed class BooleanToActiveStatusTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "Activo" : "Inactivo";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
