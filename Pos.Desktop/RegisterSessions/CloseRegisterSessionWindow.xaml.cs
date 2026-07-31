using System.Windows;
using Pos.Application.RegisterSessions;

namespace Pos.Desktop.RegisterSessions;

public partial class CloseRegisterSessionWindow : Window
{
    private readonly CloseRegisterSessionViewModel _viewModel;

    public CloseRegisterSessionWindow(CloseRegisterSessionViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _viewModel.RegisterClosed += OnRegisterClosed;
        _viewModel.CancelRequested += OnCancelRequested;

        DataContext = _viewModel;

        Closed += OnWindowClosed;
    }

    // Se establece únicamente cuando DialogResult es true; App.xaml.cs la usa para mostrar el
    // resumen final sin depender de que la ventana siga viva.
    public RegisterSessionSummary? ClosedSummary { get; private set; }

    private void OnRegisterClosed(object? sender, RegisterSessionSummary summary)
    {
        ClosedSummary = summary;
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
        _viewModel.RegisterClosed -= OnRegisterClosed;
        _viewModel.CancelRequested -= OnCancelRequested;
        Closed -= OnWindowClosed;
    }
}
