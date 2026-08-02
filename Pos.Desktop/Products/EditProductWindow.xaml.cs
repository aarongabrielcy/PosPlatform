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
        _viewModel.AdjustInventoryRequested += OnAdjustInventoryRequested;

        DataContext = _viewModel;

        Closed += OnWindowClosed;
    }

    // Reenviado hasta App.xaml.cs, único lugar que resuelve y muestra AdjustInventoryWindow desde
    // el contenedor de DI; el ViewModel nunca abre ventanas por sí mismo.
    public event EventHandler? AdjustInventoryRequested;

    public ProductId ProductId => _viewModel.ProductId;

    public string ProductName => _viewModel.Name;

    public decimal CurrentQuantity => _viewModel.CurrentQuantity;

    // SKU del producto cargado; disponible en cuanto LoadAsync termina exitosamente (el SKU es
    // read-only y nunca cambia). App.xaml.cs lo usa para refrescar el buscador de MainWindow al
    // cerrar esta ventana, sin importar si hubo cambios.
    public string? LoadedSku => _viewModel.Sku.Length > 0 ? _viewModel.Sku : null;

    public Task LoadAsync(ProductId productId) => _viewModel.LoadAsync(productId);

    // Llamado desde App.xaml.cs tras confirmar un ajuste en AdjustInventoryWindow.
    public void ApplyInventoryAdjusted(decimal newQuantity) => _viewModel.ApplyInventoryAdjusted(newQuantity);

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

    private void OnAdjustInventoryRequested(object? sender, EventArgs e) =>
        AdjustInventoryRequested?.Invoke(this, EventArgs.Empty);

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _viewModel.Saved -= OnSaved;
        _viewModel.CancelRequested -= OnCancelRequested;
        _viewModel.AdjustInventoryRequested -= OnAdjustInventoryRequested;
        Closed -= OnWindowClosed;
    }
}
