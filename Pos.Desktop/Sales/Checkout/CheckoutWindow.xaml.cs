using System.Windows;
using Pos.Application.Sales.Checkout;

namespace Pos.Desktop.Sales.Checkout;

public partial class CheckoutWindow : Window
{
    private readonly CheckoutViewModel _viewModel;

    public CheckoutWindow(CheckoutViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _viewModel.CheckoutCompleted += OnCheckoutCompleted;
        _viewModel.CancelRequested += OnCancelRequested;

        DataContext = _viewModel;

        Closed += OnWindowClosed;
    }

    // Se establece únicamente cuando DialogResult es true; App.xaml.cs la usa para mostrar el
    // resumen final sin depender de que la ventana siga viva.
    public CheckoutSummary? CompletedSummary { get; private set; }

    private void OnCheckoutCompleted(object? sender, CheckoutSummary summary)
    {
        CompletedSummary = summary;
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
        _viewModel.CheckoutCompleted -= OnCheckoutCompleted;
        _viewModel.CancelRequested -= OnCancelRequested;
        Closed -= OnWindowClosed;
    }
}
