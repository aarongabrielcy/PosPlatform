using System.Windows;
using Pos.Domain.Common.Identifiers;

namespace Pos.Desktop.Products;

public partial class AdjustInventoryWindow : Window
{
    private readonly AdjustInventoryViewModel _viewModel;

    public AdjustInventoryWindow(AdjustInventoryViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _viewModel.Confirmed += OnConfirmed;
        _viewModel.CancelRequested += OnCancelRequested;

        DataContext = _viewModel;

        Closed += OnWindowClosed;
    }

    // Se establece únicamente cuando DialogResult es true; EditProductWindow la usa para
    // actualizar la existencia mostrada sin volver a consultar el servicio.
    public decimal? NewQuantity { get; private set; }

    public void Load(ProductId productId, string productName, decimal currentQuantity) =>
        _viewModel.Load(productId, productName, currentQuantity);

    private void OnConfirmed(object? sender, EventArgs e)
    {
        NewQuantity = _viewModel.ConfirmedNewQuantity;
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
        _viewModel.Confirmed -= OnConfirmed;
        _viewModel.CancelRequested -= OnCancelRequested;
        Closed -= OnWindowClosed;
    }
}
