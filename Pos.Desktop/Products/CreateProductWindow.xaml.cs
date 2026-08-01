using System.Windows;

namespace Pos.Desktop.Products;

public partial class CreateProductWindow : Window
{
    private readonly CreateProductViewModel _viewModel;

    public CreateProductWindow(CreateProductViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _viewModel.ProductCreated += OnProductCreated;
        _viewModel.CancelRequested += OnCancelRequested;

        DataContext = _viewModel;

        Closed += OnWindowClosed;
    }

    // Se establece únicamente cuando DialogResult es true; App.xaml.cs la usa para actualizar el
    // buscador de MainWindow sin volver a consultar el producto creado.
    public string? CreatedSku { get; private set; }

    private void OnProductCreated(object? sender, string sku)
    {
        CreatedSku = sku;
        DialogResult = true;
        Close();
    }

    private void OnCancelRequested(object? sender, EventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _viewModel.ProductCreated -= OnProductCreated;
        _viewModel.CancelRequested -= OnCancelRequested;
        Closed -= OnWindowClosed;
    }
}
