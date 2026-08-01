using System.Globalization;
using System.Windows.Data;
using Pos.Application.SalesCart;

namespace Pos.Desktop.Common;

// Traduce TracksInventory/IsAvailable (ya calculados por ProductSearchResult) a un texto de
// estado para la columna "Estado" del buscador. No agrega ningún estado nuevo en Application.
public sealed class ProductAvailabilityStatusConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ProductSearchResult result)
        {
            return string.Empty;
        }

        if (!result.TracksInventory)
        {
            return "No controla inventario";
        }

        return result.IsAvailable ? string.Empty : "Sin existencia";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
