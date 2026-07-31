using System.Globalization;
using System.Windows.Input;
using Pos.Application.Authentication;
using Pos.Application.RegisterSessions;
using Pos.Desktop.Common;

namespace Pos.Desktop.Main;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly ICurrentUserSession _session;
    private readonly ICurrentRegisterSession _registerSession;
    private readonly AsyncRelayCommand _logoutCommand;
    private readonly AsyncRelayCommand _closeRegisterCommand;
    private string? _logoutBlockedMessage;

    public MainWindowViewModel(ICurrentUserSession session, ICurrentRegisterSession registerSession)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _registerSession = registerSession ?? throw new ArgumentNullException(nameof(registerSession));
        _logoutCommand = new AsyncRelayCommand(ExecuteLogoutAsync);
        _closeRegisterCommand = new AsyncRelayCommand(ExecuteCloseRegisterAsync);
    }

    public event EventHandler? LogoutRequested;

    public event EventHandler? CloseRegisterRequested;

    public ICommand LogoutCommand => _logoutCommand;

    public ICommand CloseRegisterCommand => _closeRegisterCommand;

    // Sesión ausente produce un estado seguro: cadenas vacías en lugar de excepción.
    public string DisplayName => _session.CurrentUser?.DisplayName ?? string.Empty;

    public string RoleName => _session.CurrentUser?.RoleName ?? string.Empty;

    public bool IsRegisterOpen => _registerSession.IsOpen;

    public string RegisterName => _registerSession.Current?.RegisterName ?? string.Empty;

    public string RegisterOpenedAtText => _registerSession.Current is { } session
        ? session.OpenedAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
        : string.Empty;

    public string RegisterOpeningAmountText => _registerSession.Current is { } session
        ? $"{session.OpeningAmount.ToString("N2", CultureInfo.CurrentCulture)} {session.Currency}"
        : string.Empty;

    public string RegisterStatusText => IsRegisterOpen ? "Caja abierta" : string.Empty;

    public string? LogoutBlockedMessage
    {
        get => _logoutBlockedMessage;
        private set => SetProperty(ref _logoutBlockedMessage, value);
    }

    private Task ExecuteLogoutAsync()
    {
        if (_registerSession.IsOpen)
        {
            LogoutBlockedMessage = "Debes cerrar la caja antes de cerrar sesión.";
            return Task.CompletedTask;
        }

        LogoutBlockedMessage = null;
        _session.Clear();
        LogoutRequested?.Invoke(this, EventArgs.Empty);

        return Task.CompletedTask;
    }

    private Task ExecuteCloseRegisterAsync()
    {
        CloseRegisterRequested?.Invoke(this, EventArgs.Empty);

        return Task.CompletedTask;
    }
}
