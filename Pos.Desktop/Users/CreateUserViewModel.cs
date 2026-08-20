using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Pos.Application.Users.UserManagement;
using Pos.Desktop.Common;

namespace Pos.Desktop.Users;

public sealed partial class CreateUserViewModel : ViewModelBase
{
    private readonly IUserManagementService _userManagementService;
    private readonly ILogger<CreateUserViewModel> _logger;
    private readonly AsyncRelayCommand _loadRolesCommand;
    private readonly AsyncRelayCommand _saveCommand;
    private readonly AsyncRelayCommand _cancelCommand;

    private string _username = string.Empty;
    private string _displayName = string.Empty;
    private RoleOption? _selectedRole;
    private bool _isBusy;
    private string? _generalError;

    // Igual patrón que LoginViewModel: el code-behind asigna la contraseña leyendo el PasswordBox
    // justo antes de ejecutar SaveCommand, nunca se mantiene fuera de esa ejecución.
    private string _pendingPassword = string.Empty;
    private string _pendingConfirmPassword = string.Empty;

    public CreateUserViewModel(IUserManagementService userManagementService, ILogger<CreateUserViewModel> logger)
    {
        _userManagementService = userManagementService ?? throw new ArgumentNullException(nameof(userManagementService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _loadRolesCommand = new AsyncRelayCommand(ExecuteLoadRolesAsync, () => !IsBusy, HandleUnexpectedError);
        _saveCommand = new AsyncRelayCommand(ExecuteSaveAsync, () => !IsBusy, HandleUnexpectedError);
        _cancelCommand = new AsyncRelayCommand(ExecuteCancelAsync, () => !IsBusy);

        Roles = new ObservableCollection<RoleOption>();
    }

    public event EventHandler? UserCreated;

    public event EventHandler? CancelRequested;

    public CancellationToken CancellationToken { get; set; }

    public ICommand LoadRolesCommand => _loadRolesCommand;

    public ICommand SaveCommand => _saveCommand;

    public ICommand CancelCommand => _cancelCommand;

    public ObservableCollection<RoleOption> Roles { get; }

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public RoleOption? SelectedRole
    {
        get => _selectedRole;
        set => SetProperty(ref _selectedRole, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
                _saveCommand.RaiseCanExecuteChanged();
                _cancelCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsNotBusy => !IsBusy;

    public string? GeneralError
    {
        get => _generalError;
        private set => SetProperty(ref _generalError, value);
    }

    public void SetPendingPassword(string password, string confirmPassword)
    {
        _pendingPassword = password ?? string.Empty;
        _pendingConfirmPassword = confirmPassword ?? string.Empty;
    }

    private async Task ExecuteLoadRolesAsync()
    {
        IsBusy = true;

        try
        {
            var roles = await _userManagementService.GetAssignableRolesAsync(CancellationToken);

            Roles.Clear();

            foreach (var role in roles)
            {
                Roles.Add(role);
            }

            SelectedRole ??= Roles.FirstOrDefault();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExecuteSaveAsync()
    {
        GeneralError = null;

        var password = _pendingPassword;
        var confirmPassword = _pendingConfirmPassword;
        _pendingPassword = string.Empty;
        _pendingConfirmPassword = string.Empty;

        var trimmedUsername = Username.Trim();

        if (string.IsNullOrWhiteSpace(trimmedUsername))
        {
            GeneralError = "El usuario es obligatorio.";
            return;
        }

        var trimmedDisplayName = DisplayName.Trim();

        if (string.IsNullOrWhiteSpace(trimmedDisplayName))
        {
            GeneralError = "El nombre es obligatorio.";
            return;
        }

        if (SelectedRole is null)
        {
            GeneralError = "Seleccione un rol.";
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            GeneralError = "La contraseña es obligatoria.";
            return;
        }

        if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
        {
            GeneralError = "Las contraseñas no coinciden.";
            return;
        }

        IsBusy = true;

        try
        {
            var request = new CreateUserRequest(trimmedUsername, trimmedDisplayName, SelectedRole.RoleId, password);
            var result = await _userManagementService.CreateUserAsync(request, CancellationToken);

            if (result.Success)
            {
                UserCreated?.Invoke(this, EventArgs.Empty);
                return;
            }

            GeneralError = result.Status switch
            {
                CreateUserResultStatus.NotAuthenticated => "La sesión no está disponible. Inicie sesión nuevamente.",
                CreateUserResultStatus.NotAuthorized => "No tiene permiso para crear usuarios.",
                CreateUserResultStatus.InvalidUsername => "El usuario no es válido.",
                CreateUserResultStatus.InvalidDisplayName => "El nombre no es válido.",
                CreateUserResultStatus.InvalidPassword => "La contraseña debe tener entre 8 y 256 caracteres.",
                CreateUserResultStatus.InvalidRole => "Seleccione un rol válido.",
                CreateUserResultStatus.DuplicateUsername => "Ya existe un usuario con ese nombre de usuario.",
                _ => "Ocurrió un error inesperado. Intente nuevamente.",
            };
        }
        catch (OperationCanceledException)
        {
            // Ventana cerrada mientras la operación estaba en curso: no queda UI que actualizar.
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task ExecuteCancelAsync()
    {
        CancelRequested?.Invoke(this, EventArgs.Empty);

        return Task.CompletedTask;
    }

    private void HandleUnexpectedError(Exception exception)
    {
        LogUnexpectedError(_logger, exception);
        GeneralError = "Ocurrió un error inesperado. Intente nuevamente.";
        IsBusy = false;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Error inesperado al crear el usuario.")]
    private static partial void LogUnexpectedError(ILogger logger, Exception exception);
}
