using System.Windows.Input;
using Pos.Application.Enforcement;
using Pos.Desktop.Common;

namespace Pos.Desktop.Enforcement;

// Pantalla de suspensión (secciones 4/22 de la tarea): no ofrece ningún botón de "reintentar" que
// dispare una consulta manual —el heartbeat en segundo plano (InstallationHeartbeatBackgroundService)
// sigue corriendo mientras esta ventana está abierta (ShowDialog solo anida el bucle de mensajes de
// WPF, no detiene los BackgroundService del Host) y ya reintenta cada 60s por su cuenta. Esta
// ViewModel solo escucha IInstallationEnforcementStateService.StateChanged y se resuelve sola en
// cuanto un heartbeat exitoso limpia Suspended, sin sondeo activo (sección 22: "no crear un bucle de
// sondeo").
public sealed class SuspensionViewModel : ViewModelBase, IDisposable
{
    private readonly IInstallationEnforcementStateService _enforcementStateService;
    private readonly bool _isResolvedOnLoad;
    private readonly string _message =
        "La instalación está suspendida.\n\n" +
        "La operación del punto de venta está temporalmente restringida. Contacte al administrador.\n\n" +
        "Esta ventana se cerrará automáticamente cuando la instalación vuelva a estar disponible.";

    // Capturado en el hilo que construye esta ViewModel en vez de depender de
    // System.Windows.Application.Current (ver MainWindowViewModel para la misma justificación).
    private readonly System.Windows.Threading.Dispatcher _dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
    private readonly AsyncRelayCommand _closeOpenRegisterCommand;

    private bool _disposed;

    public SuspensionViewModel(IInstallationEnforcementStateService enforcementStateService)
    {
        _enforcementStateService = enforcementStateService ?? throw new ArgumentNullException(nameof(enforcementStateService));
        _enforcementStateService.StateChanged += OnEnforcementStateChanged;

        // Si el estado ya se resolvió entre que StartupFlowCoordinator decidió mostrar esta ventana
        // y que el constructor corre, se resuelve de inmediato en vez de esperar el próximo heartbeat.
        _isResolvedOnLoad = _enforcementStateService.Current == InstallationEnforcementState.Allowed;

        _closeOpenRegisterCommand = new AsyncRelayCommand(ExecuteCloseOpenRegisterAsync);
    }

    public event EventHandler? Resolved;

    // Corrección: permite cerrar una caja que ya estaba abierta sin exponer el resto del POS
    // (sección 9 de la tarea de corrección). App.xaml.cs es quien orquesta login + cierre, igual
    // que con NewProductRequested/CheckoutRequested en MainWindowViewModel: esta ViewModel solo
    // pide la acción, nunca resuelve ventanas por sí misma.
    public event EventHandler? CloseOpenRegisterRequested;

    public ICommand CloseOpenRegisterCommand => _closeOpenRegisterCommand;

    public string Message => _message;

    // Permite a la ventana comprobar, justo después de suscribirse a Loaded, si ya debe cerrarse.
    public bool ShouldResolveImmediately => _isResolvedOnLoad;

    private Task ExecuteCloseOpenRegisterAsync()
    {
        CloseOpenRegisterRequested?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    private void OnEnforcementStateChanged(object? sender, InstallationEnforcementState state)
    {
        if (state != InstallationEnforcementState.Allowed)
        {
            return;
        }

        // StateChanged se dispara desde InstallationHeartbeatBackgroundService, un hilo distinto al
        // de UI: las suscripciones de binding de WPF exigen que los cambios lleguen desde el hilo de
        // UI.
        _dispatcher.Invoke(() => Resolved?.Invoke(this, EventArgs.Empty));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _enforcementStateService.StateChanged -= OnEnforcementStateChanged;
        _disposed = true;
    }
}
