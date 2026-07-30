using System.Windows.Input;
using Pos.Application.Authentication;
using Pos.Desktop.Common;

namespace Pos.Desktop.Main;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly ICurrentUserSession _session;
    private readonly AsyncRelayCommand _logoutCommand;

    public MainWindowViewModel(ICurrentUserSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _logoutCommand = new AsyncRelayCommand(ExecuteLogoutAsync);
    }

    public event EventHandler? LogoutRequested;

    public ICommand LogoutCommand => _logoutCommand;

    // Sesión ausente produce un estado seguro: cadenas vacías en lugar de excepción.
    public string DisplayName => _session.CurrentUser?.DisplayName ?? string.Empty;

    public string RoleName => _session.CurrentUser?.RoleName ?? string.Empty;

    private Task ExecuteLogoutAsync()
    {
        _session.Clear();
        LogoutRequested?.Invoke(this, EventArgs.Empty);

        return Task.CompletedTask;
    }
}
