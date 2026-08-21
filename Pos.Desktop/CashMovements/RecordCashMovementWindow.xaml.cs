using System.Windows;
using Pos.Application.CashMovements;
using Pos.Domain.CashMovements;

namespace Pos.Desktop.CashMovements;

public partial class RecordCashMovementWindow : Window
{
    private readonly RecordCashMovementViewModel _viewModel;

    public RecordCashMovementWindow(RecordCashMovementViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _viewModel.Confirmed += OnConfirmed;
        _viewModel.CancelRequested += OnCancelRequested;

        DataContext = _viewModel;

        Closed += OnWindowClosed;
    }

    // Se establece únicamente cuando DialogResult es true; App.xaml.cs la usa para refrescar la
    // lista de movimientos de Caja sin volver a consultar el servicio.
    public CashMovementEntry? RecordedMovement { get; private set; }

    public void Load(CashMovementType type) => _viewModel.Load(type);

    private void OnConfirmed(object? sender, CashMovementEntry movement)
    {
        RecordedMovement = movement;
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
