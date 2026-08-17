using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Pos.Application.Activation;
using Pos.Desktop.Common;

namespace Pos.Desktop.Activation;

public sealed partial class ActivationViewModel : ViewModelBase
{
    private readonly IInstallationActivationStateService _activationStateService;
    private readonly ILogger<ActivationViewModel> _logger;
    private readonly AsyncRelayCommand _activateCommand;

    private string _enrollmentCode = string.Empty;
    private bool _isBusy;
    private string? _generalError;

    public ActivationViewModel(IInstallationActivationStateService activationStateService, ILogger<ActivationViewModel> logger)
    {
        _activationStateService = activationStateService ?? throw new ArgumentNullException(nameof(activationStateService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _activateCommand = new AsyncRelayCommand(ExecuteActivateAsync, () => !IsBusy, HandleUnexpectedError);
    }

    public event EventHandler? ActivationCompleted;

    public ICommand ActivateCommand => _activateCommand;

    public CancellationToken CancellationToken { get; set; }

    public string EnrollmentCode
    {
        get => _enrollmentCode;
        set => SetProperty(ref _enrollmentCode, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
                _activateCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsNotBusy => !IsBusy;

    public string? GeneralError
    {
        get => _generalError;
        private set => SetProperty(ref _generalError, value);
    }

    private async Task ExecuteActivateAsync()
    {
        GeneralError = null;

        if (string.IsNullOrWhiteSpace(EnrollmentCode))
        {
            GeneralError = "Ingrese el código de activación.";
            return;
        }

        IsBusy = true;

        try
        {
            var outcome = await _activationStateService.EnrollAsync(EnrollmentCode, CancellationToken).ConfigureAwait(true);

            switch (outcome.Status)
            {
                case EnrollmentOutcomeStatus.Activated:
                    ActivationCompleted?.Invoke(this, EventArgs.Empty);
                    break;
                case EnrollmentOutcomeStatus.InvalidInput:
                    GeneralError = "Ingrese el código de activación.";
                    break;
                case EnrollmentOutcomeStatus.EnrollmentRejected:
                    GeneralError = "El código de activación no es válido, ya fue utilizado o expiró. " +
                        "Solicite un nuevo código al administrador.";
                    break;
                case EnrollmentOutcomeStatus.NetworkFailure:
                    GeneralError = "No fue posible conectar con el servidor de activación. " +
                        "Verifique su conexión a internet e intente nuevamente.";
                    break;
                case EnrollmentOutcomeStatus.LocalPersistenceFailed:
                    GeneralError = "La activación se procesó en el servidor, pero no fue posible guardarla en " +
                        "este equipo. El código ya no es válido: contacte a soporte técnico para obtener un " +
                        "nuevo código de recuperación.";
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

    [LoggerMessage(Level = LogLevel.Error, Message = "Error inesperado durante la activación de la instalación.")]
    private static partial void LogUnexpectedError(ILogger logger, Exception exception);
}
