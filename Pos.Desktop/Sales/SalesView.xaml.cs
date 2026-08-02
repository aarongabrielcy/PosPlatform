using System.Windows;
using System.Windows.Controls;

namespace Pos.Desktop.Sales;

// Instanciada automáticamente por el DataTemplate del shell (MainWindow) cuando CurrentViewModel
// es un SalesViewModel: nunca se resuelve desde el contenedor de DI ni se construye manualmente.
public partial class SalesView : UserControl
{
    private SalesViewModel? _viewModel;

    public SalesView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.CancelSaleConfirmationRequested -= OnCancelSaleConfirmationRequested;
        }

        _viewModel = e.NewValue as SalesViewModel;

        if (_viewModel is not null)
        {
            _viewModel.CancelSaleConfirmationRequested += OnCancelSaleConfirmationRequested;
        }
    }

    // El ViewModel nunca muestra ventanas ni MessageBox: solo pide confirmación. Confirmar o
    // cancelar el diálogo es responsabilidad exclusiva del código detrás de la vista.
    private void OnCancelSaleConfirmationRequested(object? sender, EventArgs e)
    {
        var result = MessageBox.Show(
            "¿Deseas cancelar la venta actual? Se perderán los productos agregados al carrito.",
            "PosPlatform",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            _viewModel?.ConfirmCancelSale();
        }
    }
}
