using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Pos.Application.Activation;
using Pos.Desktop.Common;

namespace Pos.Desktop.Enforcement;

// Flujo de reactivación como nueva Installation (secciones 9-10/23 de la tarea): mismo patrón que
// ActivationViewModel, pero canjea un Enrollment Code nuevo mediante ActivateAsNewInstallationAsync
// en lugar de EnrollAsync. Reemplaza tanto la credencial como el InstallationId local; los datos de
// negocio en SQLite (productos, inventario, historial) permanecen intactos.
public sealed partial class NewInstallationActivationViewModel : ViewModelBase
{
    private readonly IInstallationActivationStateService _activationStateService;
    private readonly ILogger<NewInstallationActivationViewModel> _logger;
    private readonly AsyncRelayCommand _activateCommand;
    private readonly AsyncRelayCommand _closeOpenRegisterCommand;

    private string _enrollmentCode = string.Empty;
    private bool _isBusy;
    private string? _generalError;

    public NewInstallationActivationViewModel(
        IInstallationActivationStateService activationStateService, ILogger<NewInstallationActivationViewModel> logger)
    {
        _activationStateService = activationStateService ?? throw new ArgumentNullException(nameof(activationStateService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _activateCommand = new AsyncRelayCommand(ExecuteActivateAsync, () => !IsBusy, HandleUnexpectedError);
        _closeOpenRegisterCommand = new AsyncRelayCommand(ExecuteCloseOpenRegisterAsync, () => !IsBusy);
    }

    public event EventHandler? ActivationCompleted;

    // Corrección: permite cerrar una caja que ya estaba abierta sin exponer el resto del POS
    // (sección 9 de la tarea de corrección). App.xaml.cs orquesta login + cierre; esta ViewModel
    // solo pide la acción.
    public event EventHandler? CloseOpenRegisterRequested;

    public ICommand ActivateCommand => _activateCommand;

    public ICommand CloseOpenRegisterCommand => _closeOpenRegisterCommand;

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
            var outcome = await _activationStateService
                .ActivateAsNewInstallationAsync(EnrollmentCode, CancellationToken)
                .ConfigureAwait(true);

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
                        "nuevo código.";
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

    [LoggerMessage(Level = LogLevel.Error, Message = "Error inesperado durante la activación como nueva instalación.")]
    private static partial void LogUnexpectedError(ILogger logger, Exception exception);
}
