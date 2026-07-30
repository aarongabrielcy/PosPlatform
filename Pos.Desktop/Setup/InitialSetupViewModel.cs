using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Pos.Application.Bootstrap;
using Pos.Desktop.Common;

namespace Pos.Desktop.Setup;

public sealed partial class InitialSetupViewModel : ViewModelBase
{
    private const int MinPasswordLength = 8;
    private const int MaxPasswordLength = 256;

    private readonly IInitialBusinessBootstrapService _bootstrapService;
    private readonly ILogger<InitialSetupViewModel> _logger;
    private readonly AsyncRelayCommand _submitCommand;

    private string _organizationName = string.Empty;
    private string _branchName = string.Empty;
    private string _registerName = string.Empty;
    private string _administratorUsername = string.Empty;
    private string _administratorDisplayName = string.Empty;
    private bool _isBusy;
    private string? _generalError;

    // Solo se mantienen entre el clic en "Configurar" y el arranque de ExecuteSubmitAsync: el
    // code-behind las asigna leyendo los PasswordBox y ExecuteSubmitAsync las limpia de
    // inmediato, antes de cualquier operación asíncrona.
    private string _pendingPassword = string.Empty;
    private string _pendingConfirmPassword = string.Empty;

    public InitialSetupViewModel(IInitialBusinessBootstrapService bootstrapService, ILogger<InitialSetupViewModel> logger)
    {
        _bootstrapService = bootstrapService ?? throw new ArgumentNullException(nameof(bootstrapService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _submitCommand = new AsyncRelayCommand(ExecuteSubmitAsync, () => !IsBusy, HandleUnexpectedError);
    }

    public event EventHandler? SetupCompleted;

    public ICommand SubmitCommand => _submitCommand;

    public CancellationToken CancellationToken { get; set; }

    public string OrganizationName
    {
        get => _organizationName;
        set => SetProperty(ref _organizationName, value);
    }

    public string BranchName
    {
        get => _branchName;
        set => SetProperty(ref _branchName, value);
    }

    public string RegisterName
    {
        get => _registerName;
        set => SetProperty(ref _registerName, value);
    }

    public string AdministratorUsername
    {
        get => _administratorUsername;
        set => SetProperty(ref _administratorUsername, value);
    }

    public string AdministratorDisplayName
    {
        get => _administratorDisplayName;
        set => SetProperty(ref _administratorDisplayName, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
                _submitCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsNotBusy => !IsBusy;

    public string? GeneralError
    {
        get => _generalError;
        private set => SetProperty(ref _generalError, value);
    }

    // El code-behind invoca este método únicamente al pulsar "Configurar", leyendo
    // PasswordBox.Password en ese instante. El ViewModel nunca expone ni retiene la contraseña
    // más allá de la ejecución en curso.
    public void SetPendingCredentials(string password, string confirmPassword)
    {
        _pendingPassword = password ?? string.Empty;
        _pendingConfirmPassword = confirmPassword ?? string.Empty;
    }

    private async Task ExecuteSubmitAsync()
    {
        GeneralError = null;

        var password = _pendingPassword;
        var confirmPassword = _pendingConfirmPassword;
        _pendingPassword = string.Empty;
        _pendingConfirmPassword = string.Empty;

        var validationError = Validate(password, confirmPassword);
        if (validationError is not null)
        {
            GeneralError = validationError;
            return;
        }

        IsBusy = true;

        try
        {
            var request = new InitialBusinessBootstrapRequest(
                OrganizationName.Trim(),
                BranchName.Trim(),
                RegisterName.Trim(),
                AdministratorUsername.Trim(),
                AdministratorDisplayName.Trim(),
                password);

            var result = await _bootstrapService.BootstrapAsync(request, CancellationToken);

            if (result.Status is InitialBusinessBootstrapStatus.Created or InitialBusinessBootstrapStatus.AlreadyInitialized)
            {
                SetupCompleted?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (InitialBusinessBootstrapStateException ex)
        {
            LogInconsistentState(_logger, ex);
            GeneralError = "La instalación existente presenta un estado inconsistente. Contacte a soporte técnico.";
        }
        catch (InitialBusinessBootstrapValidationException ex)
        {
            GeneralError = ex.Message;
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

    private void HandleUnexpectedError(Exception exception)
    {
        LogUnexpectedError(_logger, exception);
        GeneralError = "Ocurrió un error inesperado. Intente nuevamente.";
        IsBusy = false;
    }

    private string? Validate(string password, string confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(OrganizationName))
        {
            return "El nombre del negocio es obligatorio.";
        }

        if (string.IsNullOrWhiteSpace(BranchName))
        {
            return "El nombre de la sucursal es obligatorio.";
        }

        if (string.IsNullOrWhiteSpace(RegisterName))
        {
            return "El nombre de la caja es obligatorio.";
        }

        if (string.IsNullOrWhiteSpace(AdministratorUsername))
        {
            return "El usuario administrador es obligatorio.";
        }

        if (string.IsNullOrWhiteSpace(AdministratorDisplayName))
        {
            return "El nombre visible del administrador es obligatorio.";
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            return "La contraseña es obligatoria.";
        }

        if (password.Length is < MinPasswordLength or > MaxPasswordLength)
        {
            return $"La contraseña debe tener entre {MinPasswordLength} y {MaxPasswordLength} caracteres.";
        }

        if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
        {
            return "Las contraseñas no coinciden.";
        }

        return null;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Estado de instalación inconsistente detectado durante la configuración inicial.")]
    private static partial void LogInconsistentState(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error inesperado durante la configuración inicial.")]
    private static partial void LogUnexpectedError(ILogger logger, Exception exception);
}
