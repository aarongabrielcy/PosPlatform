using System.Windows;

namespace Pos.Desktop.Enforcement;

public partial class SuspensionWindow : Window, IDisposable
{
    private readonly SuspensionViewModel _viewModel;
    private bool _disposed;

    public SuspensionWindow(SuspensionViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _viewModel.Resolved += OnResolved;
        _viewModel.CloseOpenRegisterRequested += OnViewModelCloseOpenRegisterRequested;

        DataContext = _viewModel;

        Loaded += OnLoaded;
        Closed += OnWindowClosed;
    }

    // Corrección: App.xaml.cs orquesta login + cierre de la caja abierta sin cerrar esta ventana
    // (sección 9 de la tarea de corrección), igual patrón que MainWindow bubblea los eventos de su
    // ViewModel hacia App.xaml.cs.
    public event EventHandler? CloseOpenRegisterRequested;

    // El estado pudo resolverse (heartbeat exitoso) entre que StartupFlowCoordinator decidió mostrar
    // esta ventana y que Loaded corre: se cierra de inmediato en vez de obligar al usuario a ver una
    // pantalla de suspensión ya resuelta.
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel.ShouldResolveImmediately)
        {
            DialogResult = true;
            Close();
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

    private void OnResolved(object? sender, EventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _viewModel.Resolved -= OnResolved;
        _viewModel.CloseOpenRegisterRequested -= OnViewModelCloseOpenRegisterRequested;
        Dispose();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _viewModel.Dispose();
        _disposed = true;

        GC.SuppressFinalize(this);
    }
}
