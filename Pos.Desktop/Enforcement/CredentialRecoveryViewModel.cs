using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Pos.Application.Activation;
using Pos.Desktop.Common;

namespace Pos.Desktop.Enforcement;

// Flujo de recuperación de credencial (secciones 6-8/24 de la tarea): mismo patrón que
// ActivationViewModel, pero canjea un Recovery Enrollment Code mediante RecoverCredentialAsync en
// lugar de EnrollAsync. La Installation conserva su identidad; solo se reemplaza la credencial.
public sealed partial class CredentialRecoveryViewModel : ViewModelBase
{
    private readonly IInstallationActivationStateService _activationStateService;
    private readonly ILogger<CredentialRecoveryViewModel> _logger;
    private readonly AsyncRelayCommand _reactivateCommand;
    private readonly AsyncRelayCommand _closeOpenRegisterCommand;

    private string _recoveryCode = string.Empty;
    private bool _isBusy;
    private string? _generalError;

    public CredentialRecoveryViewModel(
        IInstallationActivationStateService activationStateService, ILogger<CredentialRecoveryViewModel> logger)
    {
        _activationStateService = activationStateService ?? throw new ArgumentNullException(nameof(activationStateService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _reactivateCommand = new AsyncRelayCommand(ExecuteReactivateAsync, () => !IsBusy, HandleUnexpectedError);
        _closeOpenRegisterCommand = new AsyncRelayCommand(ExecuteCloseOpenRegisterAsync, () => !IsBusy);
    }

    public event EventHandler? RecoveryCompleted;

    // Corrección: permite cerrar una caja que ya estaba abierta sin exponer el resto del POS
    // (sección 9 de la tarea de corrección). App.xaml.cs orquesta login + cierre; esta ViewModel
    // solo pide la acción.
    public event EventHandler? CloseOpenRegisterRequested;

    public ICommand ReactivateCommand => _reactivateCommand;

    public ICommand CloseOpenRegisterCommand => _closeOpenRegisterCommand;

    public CancellationToken CancellationToken { get; set; }

    public string RecoveryCode
    {
        get => _recoveryCode;
        set => SetProperty(ref _recoveryCode, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
                _reactivateCommand.RaiseCanExecuteChanged();
                _closeOpenRegisterCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsNotBusy => !IsBusy;

    public string? GeneralError
    {
        get => _generalError;
        private set => SetProperty(ref _generalError, value);
    }

    private async Task ExecuteReactivateAsync()
    {
        GeneralError = null;

        if (string.IsNullOrWhiteSpace(RecoveryCode))
        {
            GeneralError = "Ingrese el código de recuperación.";
            return;
        }

        IsBusy = true;

        try
        {
            var outcome = await _activationStateService.RecoverCredentialAsync(RecoveryCode, CancellationToken).ConfigureAwait(true);

            switch (outcome.Status)
            {
                case EnrollmentOutcomeStatus.Activated:
                    RecoveryCompleted?.Invoke(this, EventArgs.Empty);
                    break;
                case EnrollmentOutcomeStatus.InvalidInput:
                    GeneralError = "Ingrese el código de recuperación.";
                    break;
                case EnrollmentOutcomeStatus.EnrollmentRejected:
                    GeneralError = "El código de recuperación no es válido, ya fue utilizado o expiró. " +
                        "Solicite un nuevo código al administrador.";
                    break;
                case EnrollmentOutcomeStatus.NetworkFailure:
                    GeneralError = "No fue posible conectar con el servidor. " +
                        "Verifique su conexión a internet e intente nuevamente.";
                    break;
                case EnrollmentOutcomeStatus.LocalPersistenceFailed:
                    GeneralError = "La recuperación se procesó en el servidor, pero no fue posible guardarla en " +
                        "este equipo. Contacte a soporte técnico.";
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

    private Task ExecuteCloseOpenRegisterAsync()
    {
        CloseOpenRegisterRequested?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    private void HandleUnexpectedError(Exception exception)
    {
        LogUnexpectedError(_logger, exception);
        GeneralError = "Ocurrió un error inesperado. Intente nuevamente.";
        IsBusy = false;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Error inesperado durante la recuperación de credencial de la instalación.")]
    private static partial void LogUnexpectedError(ILogger logger, Exception exception);
}
