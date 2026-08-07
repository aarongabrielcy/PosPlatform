using System.Windows;
using Pos.Domain.Common.Identifiers;

namespace Pos.Desktop.Products;

public partial class EditProductWindow : Window
{
    private readonly EditProductViewModel _viewModel;

    public EditProductWindow(EditProductViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _viewModel.Saved += OnSaved;
        _viewModel.CancelRequested += OnCancelRequested;
        _viewModel.CartWarningRequested += OnCartWarningRequested;

        DataContext = _viewModel;

        Closed += OnWindowClosed;
    }

    // SKU del producto cargado; disponible en cuanto LoadAsync termina exitosamente (el SKU es
    // read-only y nunca cambia). App.xaml.cs lo usa para refrescar el buscador de MainWindow al
    // cerrar esta ventana, sin importar si hubo cambios.
    public string? LoadedSku => _viewModel.Sku.Length > 0 ? _viewModel.Sku : null;

    public Task LoadAsync(ProductId productId) => _viewModel.LoadAsync(productId);

    private void OnSaved(object? sender, EventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnCancelRequested(object? sender, EventArgs e)
    {
        DialogResult = false;
        Close();
    }

    // El ViewModel nunca muestra MessageBox: solo pide advertir que el producto sigue en la venta
    // actual tras cambiar su SKU. Se muestra antes de que Saved cierre la ventana (TAREA 24C,
    // sección 15); no bloquea el guardado, que ya ocurrió.
    private void OnCartWarningRequested(object? sender, EventArgs e)
    {
        MessageBox.Show(
            "El producto está en la venta actual. La línea conserva los datos con los que fue agregada.",
            "PosPlatform",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _viewModel.Saved -= OnSaved;
        _viewModel.CancelRequested -= OnCancelRequested;
        _viewModel.CartWarningRequested -= OnCartWarningRequested;
        Closed -= OnWindowClosed;
    }
}
