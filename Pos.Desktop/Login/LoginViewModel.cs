using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Pos.Application.Authentication;
using Pos.Desktop.Common;

namespace Pos.Desktop.Login;

public sealed partial class LoginViewModel : ViewModelBase
{
    private readonly IAuthenticationService _authenticationService;
    private readonly ILogger<LoginViewModel> _logger;
    private readonly AsyncRelayCommand _loginCommand;

    private string _username = string.Empty;
    private bool _isBusy;
    private string? _generalError;

    // Solo se mantiene entre el clic en "Iniciar sesión" y el arranque de ExecuteLoginAsync: el
    // code-behind la asigna leyendo el PasswordBox y ExecuteLoginAsync la limpia de inmediato,
    // antes de cualquier operación asíncrona.
    private string _pendingPassword = string.Empty;

    public LoginViewModel(IAuthenticationService authenticationService, ILogger<LoginViewModel> logger)
    {
        _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _loginCommand = new AsyncRelayCommand(ExecuteLoginAsync, () => !IsBusy, HandleUnexpectedError);
    }

    public event EventHandler? LoginSucceeded;

    public ICommand LoginCommand => _loginCommand;

    public CancellationToken CancellationToken { get; set; }

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
                _loginCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsNotBusy => !IsBusy;

    public string? GeneralError
    {
        get => _generalError;
        private set => SetProperty(ref _generalError, value);
    }

    // El code-behind invoca este método únicamente al pulsar "Iniciar sesión", leyendo
    // PasswordBox.Password en ese instante. El ViewModel nunca expone ni retiene la contraseña
    // más allá de la ejecución en curso.
    public void SetPendingPassword(string password)
    {
        _pendingPassword = password ?? string.Empty;
    }

    private async Task ExecuteLoginAsync()
    {
        GeneralError = null;

        var password = _pendingPassword;
        _pendingPassword = string.Empty;

        var validationError = Validate(Username, password);
        if (validationError is not null)
        {
            GeneralError = validationError;
            return;
        }

        IsBusy = true;

        try
        {
            var request = new AuthenticationRequest(Username.Trim(), password);
            var result = await _authenticationService.AuthenticateAsync(request, CancellationToken);

            switch (result.Status)
            {
                case AuthenticationStatus.Success:
                    LoginSucceeded?.Invoke(this, EventArgs.Empty);
                    break;
                case AuthenticationStatus.InvalidCredentials:
                    GeneralError = "Usuario o contraseña incorrectos.";
                    break;
                case AuthenticationStatus.InactiveUser:
                case AuthenticationStatus.InactiveRole:
                    GeneralError = "La cuenta no está disponible. Contacta al administrador.";
                    break;
                case AuthenticationStatus.InvalidInstallationState:
                    GeneralError = "La instalación presenta una configuración inválida.";
                    break;
                default:
                    GeneralError = "Ocurrió un error inesperado. Intente nuevamente.";
                    break;
            }
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

    private static string? Validate(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return "El usuario es obligatorio.";
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            return "La contraseña es obligatoria.";
        }

        return null;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Error inesperado durante el inicio de sesión.")]
    private static partial void LogUnexpectedError(ILogger logger, Exception exception);
}
