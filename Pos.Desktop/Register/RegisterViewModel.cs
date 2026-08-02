using System.Globalization;
using System.Windows.Input;
using Pos.Application.RegisterSessions;
using Pos.Desktop.Common;

namespace Pos.Desktop.Register;

// Resumen de caja (TAREA 24C, sección 21): reutiliza ICurrentRegisterSession, la misma fuente que
// alimenta el encabezado compacto del shell. Cerrar caja se resuelve igual que en
// MainWindowViewModel: el ViewModel nunca abre ventanas, solo pide cerrar.
public sealed class RegisterViewModel : ViewModelBase
{
    private readonly ICurrentRegisterSession _registerSession;
    private readonly AsyncRelayCommand _closeRegisterCommand;

    public RegisterViewModel(ICurrentRegisterSession registerSession)
    {
        _registerSession = registerSession ?? throw new ArgumentNullException(nameof(registerSession));

        _closeRegisterCommand = new AsyncRelayCommand(ExecuteCloseRegisterAsync);
    }

    // El shell (MainWindowViewModel) reenvía el cierre real, aplicando el mismo bloqueo por
    // carrito con líneas que ya existía en MainWindowViewModel.
    public event EventHandler? CloseRegisterRequested;

    public ICommand CloseRegisterCommand => _closeRegisterCommand;

    public bool IsRegisterOpen => _registerSession.IsOpen;

    public string RegisterName => _registerSession.Current?.RegisterName ?? string.Empty;

    public string OpenedByDisplayName => _registerSession.Current?.OpenedByDisplayName ?? string.Empty;

    public string OpenedAtText => _registerSession.Current is { } session
        ? session.OpenedAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
        : string.Empty;

    public string OpeningAmountText => _registerSession.Current is { } session
        ? $"{session.OpeningAmount.ToString("N2", CultureInfo.CurrentCulture)} {session.Currency}"
        : string.Empty;

    public string StatusText => IsRegisterOpen ? "Caja abierta" : "Caja cerrada";

    private Task ExecuteCloseRegisterAsync()
    {
        CloseRegisterRequested?.Invoke(this, EventArgs.Empty);

        return Task.CompletedTask;
    }
}
