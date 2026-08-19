using System.Windows;

namespace Pos.Desktop.Enforcement;

public partial class CredentialRecoveryWindow : Window, IDisposable
{
    private readonly CredentialRecoveryViewModel _viewModel;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private bool _disposed;

    public CredentialRecoveryWindow(CredentialRecoveryViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _viewModel.CancellationToken = _cancellationTokenSource.Token;
        _viewModel.RecoveryCompleted += OnRecoveryCompleted;
        _viewModel.CloseOpenRegisterRequested += OnViewModelCloseOpenRegisterRequested;

        DataContext = _viewModel;

        Loaded += OnLoaded;
        Closed += OnWindowClosed;
    }

    // Corrección: App.xaml.cs orquesta login + cierre de la caja abierta sin cerrar esta ventana
    // (sección 9 de la tarea de corrección).
    public event EventHandler? CloseOpenRegisterRequested;

    private void OnLoaded(object sender, RoutedEventArgs e) => RecoveryCodeTextBox.Focus();

    private void OnReactivarClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.ReactivateCommand.CanExecute(null))
        {
            _viewModel.ReactivateCommand.Execute(null);
        }
    }

    private void OnCerrarCajaAbiertaClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.CloseOpenRegisterCommand.CanExecute(null))
        {
            _viewModel.CloseOpenRegisterCommand.Execute(null);
        }
    }

    private void OnViewModelCloseOpenRegisterRequested(object? sender, EventArgs e) =>
        CloseOpenRegisterRequested?.Invoke(this, EventArgs.Empty);

    private void OnRecoveryCompleted(object? sender, EventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _viewModel.RecoveryCompleted -= OnRecoveryCompleted;
        _viewModel.CloseOpenRegisterRequested -= OnViewModelCloseOpenRegisterRequested;
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
