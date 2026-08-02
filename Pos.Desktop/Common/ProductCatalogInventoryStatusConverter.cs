using System.Globalization;
using System.Windows.Data;
using Pos.Application.Products.ManageProduct;

namespace Pos.Desktop.Common;

// Traduce TracksInventory/Quantity/ReorderPoint (ya resueltos por ProductCatalogItem) a un texto
// de estado para la columna "Inventario" del catálogo (TAREA 24C, sección 11). No inventa "Sin
// existencia" para productos que no controlan inventario.
public sealed class ProductCatalogInventoryStatusConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ProductCatalogItem item)
        {
            return string.Empty;
        }

        if (!item.TracksInventory)
        {
            return "No controla inventario";
        }

        if (item.Quantity <= 0m)
        {
            return "Sin existencia";
        }

        if (item.Quantity <= item.ReorderPoint)
        {
            return "Stock bajo";
        }

        return string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
