using System.Windows;
using Pos.Application.RegisterSessions;

namespace Pos.Desktop.RegisterSessions;

public partial class OpenRegisterSessionWindow : Window, IDisposable
{
    private readonly OpenRegisterSessionViewModel _viewModel;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private bool _disposed;

    public OpenRegisterSessionWindow(OpenRegisterSessionViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _viewModel.CancellationToken = _cancellationTokenSource.Token;
        _viewModel.RegisterOpened += OnRegisterOpened;
        _viewModel.LogoutRequested += OnLogoutRequested;

        DataContext = _viewModel;

        Loaded += OnLoaded;
        Closed += OnWindowClosed;
    }

    // Se establece únicamente cuando DialogResult es true; App.xaml.cs la usa para continuar a
    // MainWindow sin volver a consultar el estado de la caja.
    public ActiveRegisterSession? OpenedSession { get; private set; }

    // Distingue los tres desenlaces posibles de la ventana. El valor por defecto (ExitRequested)
    // cubre el cierre mediante la X, que nunca establece DialogResult ni pasa por los handlers
    // de abajo.
    public OpenRegisterSessionWindowResult Result { get; private set; } = OpenRegisterSessionWindowResult.ExitRequested;

    private async void OnLoaded(object sender, RoutedEventArgs e) => await _viewModel.InitializeAsync();

    private void OnRegisterOpened(object? sender, ActiveRegisterSession activeSession)
    {
        OpenedSession = activeSession;
        Result = OpenRegisterSessionWindowResult.RegisterOpened;
        DialogResult = true;
        Close();
    }

    private void OnLogoutRequested(object? sender, EventArgs e)
    {
        Result = OpenRegisterSessionWindowResult.LogoutRequested;
        DialogResult = false;
        Close();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _viewModel.RegisterOpened -= OnRegisterOpened;
        _viewModel.LogoutRequested -= OnLogoutRequested;
        Closed -= OnWindowClosed;

        Dispose();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (!_cancellationTokenSource.IsCancellationRequested)
        {
            _cancellationTokenSource.Cancel();
        }

        _cancellationTokenSource.Dispose();
        _disposed = true;

        GC.SuppressFinalize(this);
    }
}
