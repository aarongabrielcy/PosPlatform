using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Pos.Desktop.Main;

namespace Pos.Desktop.Common;

// Traduce NavigationSection a su Geometry (TAREA 24C.1, sección 7). Las geometrías se parsean
// directamente aquí (en vez de leerlas de Pos.Desktop/Themes/Icons.xaml vía Application.Current)
// porque las pruebas construyen MainWindow/UserControls sin crear una instancia de
// System.Windows.Application: Application.Current sería null y la búsqueda de recursos a nivel de
// aplicación fallaría solo en ese escenario.
public sealed class NavigationSectionIconConverter : IValueConverter
{
    private static readonly Geometry DashboardIcon =
        Geometry.Parse("M0,0 H7 V7 H0 Z M9,0 H16 V7 H9 Z M0,9 H7 V16 H0 Z M9,9 H16 V16 H9 Z");

    private static readonly Geometry SalesIcon = Geometry.Parse(
        "M2,3 L4,3 L6,11 L14,11 L16,5 L5,5 Z M3.7,14 A1.3,1.3 0 1,0 6.3,14 A1.3,1.3 0 1,0 3.7,14 Z M10.7,14 A1.3,1.3 0 1,0 13.3,14 A1.3,1.3 0 1,0 10.7,14 Z");

    private static readonly Geometry ProductsIcon = Geometry.Parse("M1,5 L8,2 L15,5 L15,12 L8,15 L1,12 Z");

    private static readonly Geometry InventoryIcon =
        Geometry.Parse("M8,0 L15,3 L8,6 L1,3 Z M8,5 L15,8 L8,11 L1,8 Z M8,10 L15,13 L8,16 L1,13 Z");

    private static readonly Geometry RegisterIcon = Geometry.Parse("M1,4 H15 V6 H1 Z M2,7 H14 V14 H2 Z");

    private static readonly Geometry SettingsIcon = Geometry.Parse("M8,1 A7,7 0 1,0 8.001,1 Z M8,5 A3,3 0 1,0 8.001,5 Z");

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not NavigationSection section)
        {
            return null;
        }

        return section switch
        {
            NavigationSection.Dashboard => DashboardIcon,
            NavigationSection.Sales => SalesIcon,
            NavigationSection.Products => ProductsIcon,
            NavigationSection.Inventory => InventoryIcon,
            NavigationSection.Register => RegisterIcon,
            NavigationSection.Settings => SettingsIcon,
            _ => DashboardIcon,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
