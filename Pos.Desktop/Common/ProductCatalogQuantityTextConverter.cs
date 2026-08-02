using System.Globalization;
using System.Windows.Data;
using Pos.Application.Products.ManageProduct;

namespace Pos.Desktop.Common;

// Muestra Quantity/ReorderPoint (según ConverterParameter: "Quantity" o "ReorderPoint") solo
// cuando el producto controla inventario; en caso contrario no inventa un cero ni un guion
// engañoso, muestra el texto explícito "No controla inventario" (TAREA 24C, sección 11).
public sealed class ProductCatalogQuantityTextConverter : IValueConverter
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

        var amount = string.Equals(parameter as string, "ReorderPoint", StringComparison.Ordinal)
            ? item.ReorderPoint
            : item.Quantity;

        return amount.ToString(CultureInfo.CurrentCulture);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
