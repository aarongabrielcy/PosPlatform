using System.Windows.Controls;

namespace Pos.Desktop.Products.Catalog;

// Instanciada automáticamente por el DataTemplate del shell (MainWindow) cuando CurrentViewModel
// es un ProductsViewModel: nunca se resuelve desde el contenedor de DI ni se construye manualmente.
public partial class ProductsView : UserControl
{
    public ProductsView()
    {
        InitializeComponent();
    }
}
