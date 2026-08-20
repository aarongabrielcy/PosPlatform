using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Pos.Application.Users.UserManagement;
using Pos.Desktop.Common;
using Pos.Domain.Common.Identifiers;

namespace Pos.Desktop.Users;

public sealed partial class EditUserViewModel : ViewModelBase
{
    private readonly IUserManagementService _userManagementService;
    private readonly ILogger<EditUserViewModel> _logger;
    private readonly AsyncRelayCommand _saveCommand;
    private readonly AsyncRelayCommand _toggleActiveCommand;
    private readonly AsyncRelayCommand _resetPasswordCommand;
    private readonly AsyncRelayCommand _cancelCommand;

    private UserId _userId;
    private bool _isLoaded;
    private string _username = string.Empty;
    private string _displayName = string.Empty;
    private RoleOption? _selectedRole;
    private bool _isActive;
    private bool _isBusy;
    private string? _generalError;
    private string? _passwordResetMessage;

    // Igual patrón que CreateUserViewModel/LoginViewModel: el code-behind asigna la contraseña
    // leyendo los PasswordBox justo antes de ejecutar ResetPasswordCommand.
    private string _pendingNewPassword = string.Empty;
    private string _pendingConfirmNewPassword = string.Empty;

    public EditUserViewModel(IUserManagementService userManagementService, ILogger<EditUserViewModel> logger)
    {
        _userManagementService = userManagementService ?? throw new ArgumentNullException(nameof(userManagementService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _saveCommand = new AsyncRelayCommand(ExecuteSaveAsync, () => !IsBusy && _isLoaded, HandleUnexpectedError);
        _toggleActiveCommand = new AsyncRelayCommand(ExecuteToggleActiveAsync, () => !IsBusy && _isLoaded, HandleUnexpectedError);
        _resetPasswordCommand = new AsyncRelayCommand(ExecuteResetPasswordAsync, () => !IsBusy && _isLoaded, HandleUnexpectedError);
        _cancelCommand = new AsyncRelayCommand(ExecuteCancelAsync, () => !IsBusy);

        Roles = new ObservableCollection<RoleOption>();
    }

    // Guardar cambios/activar-desactivar/restablecer contraseña exitosos: el código detrás
    // refresca la fila en la lista pero NO cierra la ventana (a diferencia de
    // CreateUserViewModel.UserCreated) - el administrador puede encadenar varias acciones sobre el
    // mismo usuario (p. ej. cambiar rol y luego restablecer contraseña) sin reabrir la ventana.
    public event EventHandler? Updated;

    public event EventHandler? CancelRequested;

    public CancellationToken CancellationToken { get; set; }

    public ICommand SaveCommand => _saveCommand;

    public ICommand ToggleActiveCommand => _toggleActiveCommand;

    public ICommand ResetPasswordCommand => _resetPasswordCommand;

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

    public bool IsActive
    {
        get => _isActive;
        private set
        {
            if (SetProperty(ref _isActive, value))
            {
                OnPropertyChanged(nameof(ToggleActiveButtonText));
                OnPropertyChanged(nameof(IsActiveText));
            }
        }
    }

    public string ToggleActiveButtonText => IsActive ? "Desactivar" : "Activar";

    public string IsActiveText => IsActive ? "Activo" : "Inactivo";

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
                _saveCommand.RaiseCanExecuteChanged();
                _toggleActiveCommand.RaiseCanExecuteChanged();
                _resetPasswordCommand.RaiseCanExecuteChanged();
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

    public string? PasswordResetMessage
    {
        get => _passwordResetMessage;
        private set => SetProperty(ref _passwordResetMessage, value);
    }

    public void SetPendingNewPassword(string newPassword, string confirmNewPassword)
    {
        _pendingNewPassword = newPassword ?? string.Empty;
        _pendingConfirmNewPassword = confirmNewPassword ?? string.Empty;
    }

    public async Task LoadAsync(UserId userId)
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

            var users = await _userManagementService.GetUsersAsync(CancellationToken);
            var target = users.FirstOrDefault(u => u.UserId == userId);

            if (target is null)
            {
                GeneralError = "No fue posible cargar el usuario.";
                return;
            }

            ApplyItem(target);
            _isLoaded = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyItem(UserListItem item)
    {
        _userId = item.UserId;
        Username = item.Username;
        DisplayName = item.DisplayName;
        IsActive = item.IsActive;

        // Si el rol actual ya no está entre los Roles activos asignables (p. ej. fue desactivado
        // después de asignarse), se agrega igual a la lista para no perder la selección visible -
        // sin eso, el ComboBox quedaría sin selección y un Guardar accidental reasignaría el rol.
        var currentRole = Roles.FirstOrDefault(r => r.RoleId == item.RoleId)
            ?? new RoleOption(item.RoleId, item.RoleName);

        if (Roles.All(r => r.RoleId != currentRole.RoleId))
        {
            Roles.Insert(0, currentRole);
        }

        SelectedRole = Roles.FirstOrDefault(r => r.RoleId == item.RoleId);
    }

    private async Task ExecuteSaveAsync()
    {
        GeneralError = null;

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

        IsBusy = true;

        try
        {
            var request = new UpdateUserRequest(_userId, trimmedUsername, trimmedDisplayName, SelectedRole.RoleId);
            var result = await _userManagementService.UpdateUserAsync(request, CancellationToken);

            if (result.Success)
            {
                ApplyItem(result.User!);
                Updated?.Invoke(this, EventArgs.Empty);
                return;
            }

            GeneralError = ToOperationErrorMessage(result.Status);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExecuteToggleActiveAsync()
    {
        GeneralError = null;
        IsBusy = true;

        try
        {
            var result = await _userManagementService.SetActiveAsync(_userId, !IsActive, CancellationToken);

            if (result.Success)
            {
                ApplyItem(result.User!);
                Updated?.Invoke(this, EventArgs.Empty);
                return;
            }

            GeneralError = ToOperationErrorMessage(result.Status);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExecuteResetPasswordAsync()
    {
        GeneralError = null;
        PasswordResetMessage = null;

        var newPassword = _pendingNewPassword;
        var confirmNewPassword = _pendingConfirmNewPassword;
        _pendingNewPassword = string.Empty;
        _pendingConfirmNewPassword = string.Empty;

        if (string.IsNullOrEmpty(newPassword))
        {
            GeneralError = "La nueva contraseña es obligatoria.";
            return;
        }

        if (!string.Equals(newPassword, confirmNewPassword, StringComparison.Ordinal))
        {
            GeneralError = "Las contraseñas no coinciden.";
            return;
        }

        IsBusy = true;

        try
        {
            var request = new ResetPasswordRequest(_userId, newPassword);
            var result = await _userManagementService.ResetPasswordAsync(request, CancellationToken);

            if (result.Success)
            {
                PasswordResetMessage = "Contraseña actualizada correctamente.";
                return;
            }

            GeneralError = ToOperationErrorMessage(result.Status);
        }
        catch (OperationCanceledException)
        {
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

    private static string ToOperationErrorMessage(UserOperationResultStatus status) => status switch
    {
        UserOperationResultStatus.NotAuthenticated => "La sesión no está disponible. Inicie sesión nuevamente.",
        UserOperationResultStatus.NotAuthorized => "No tiene permiso para administrar usuarios.",
        UserOperationResultStatus.UserNotFound => "El usuario ya no existe.",
        UserOperationResultStatus.InvalidUsername => "El usuario no es válido.",
        UserOperationResultStatus.InvalidDisplayName => "El nombre no es válido.",
        UserOperationResultStatus.InvalidRole => "Seleccione un rol válido.",
        UserOperationResultStatus.InvalidPassword => "La contraseña debe tener entre 8 y 256 caracteres.",
        UserOperationResultStatus.DuplicateUsername => "Ya existe otro usuario con ese nombre de usuario.",
        UserOperationResultStatus.CannotDeactivateLastAdmin =>
            "No puede desactivar al último administrador activo.",
        UserOperationResultStatus.CannotDemoteLastAdmin =>
            "No puede cambiar el rol del último administrador activo.",
        _ => "Ocurrió un error inesperado. Intente nuevamente.",
    };

    [LoggerMessage(Level = LogLevel.Error, Message = "Error inesperado al editar el usuario.")]
    private static partial void LogUnexpectedError(ILogger logger, Exception exception);
}
