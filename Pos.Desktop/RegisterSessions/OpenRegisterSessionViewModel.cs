using System.Globalization;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Pos.Application.Authentication;
using Pos.Application.RegisterSessions;
using Pos.Desktop.Common;

namespace Pos.Desktop.RegisterSessions;

public sealed partial class OpenRegisterSessionViewModel : ViewModelBase
{
    private const decimal MaxOpeningAmount = 999_999_999.99m;

    private readonly IRegisterSessionService _registerSessionService;
    private readonly ICurrentUserSession _currentUserSession;
    private readonly ILogger<OpenRegisterSessionViewModel> _logger;
    private readonly AsyncRelayCommand _openCommand;
    private readonly AsyncRelayCommand _logoutCommand;

    private IReadOnlyList<AvailableRegister> _availableRegisters = Array.Empty<AvailableRegister>();
    private AvailableRegister? _selectedRegister;
    private string _openingAmountText = string.Empty;
    private bool _isBusy;
    private string? _generalError;
    private bool _isInitialized;

    public OpenRegisterSessionViewModel(
        IRegisterSessionService registerSessionService,
        ICurrentUserSession currentUserSession,
        ILogger<OpenRegisterSessionViewModel> logger)
    {
        _registerSessionService = registerSessionService ?? throw new ArgumentNullException(nameof(registerSessionService));
        _currentUserSession = currentUserSession ?? throw new ArgumentNullException(nameof(currentUserSession));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _openCommand = new AsyncRelayCommand(ExecuteOpenAsync, () => !IsBusy, HandleUnexpectedError);
        _logoutCommand = new AsyncRelayCommand(ExecuteLogoutAsync, () => !IsBusy);
    }

    public event EventHandler<ActiveRegisterSession>? RegisterOpened;

    public event EventHandler? LogoutRequested;

    public CancellationToken CancellationToken { get; set; }

    public ICommand OpenCommand => _openCommand;

    public ICommand LogoutCommand => _logoutCommand;

    public string UserDisplayName => _currentUserSession.CurrentUser?.DisplayName ?? string.Empty;

    public IReadOnlyList<AvailableRegister> AvailableRegisters
    {
        get => _availableRegisters;
        private set
        {
            if (SetProperty(ref _availableRegisters, value))
            {
                OnPropertyChanged(nameof(RequiresRegisterSelection));
                OnPropertyChanged(nameof(RegisterSelectorVisibility));
                OnPropertyChanged(nameof(RegisterReadOnlyVisibility));
                OnPropertyChanged(nameof(ReadOnlyRegisterName));
            }
        }
    }

    public bool RequiresRegisterSelection => AvailableRegisters.Count > 1;

    public Visibility RegisterSelectorVisibility =>
        RequiresRegisterSelection ? Visibility.Visible : Visibility.Collapsed;

    public Visibility RegisterReadOnlyVisibility =>
        RequiresRegisterSelection ? Visibility.Collapsed : Visibility.Visible;

    public string ReadOnlyRegisterName => AvailableRegisters.Count == 1 ? AvailableRegisters[0].Name : string.Empty;

    public AvailableRegister? SelectedRegister
    {
        get => _selectedRegister;
        set => SetProperty(ref _selectedRegister, value);
    }

    public string OpeningAmountText
    {
        get => _openingAmountText;
        set => SetProperty(ref _openingAmountText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
                _openCommand.RaiseCanExecuteChanged();
                _logoutCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsNotBusy => !IsBusy;

    public string? GeneralError
    {
        get => _generalError;
        private set => SetProperty(ref _generalError, value);
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;
        IsBusy = true;

        try
        {
            var registers = await _registerSessionService.GetAvailableRegistersAsync(CancellationToken);
            AvailableRegisters = registers;

            if (registers.Count == 1)
            {
                SelectedRegister = registers[0];
            }

            if (registers.Count == 0)
            {
                GeneralError = "No hay ninguna caja activa disponible. Contacte al administrador.";
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

    private async Task ExecuteOpenAsync()
    {
        GeneralError = null;

        if (AvailableRegisters.Count == 0)
        {
            GeneralError = "No hay ninguna caja activa disponible. Contacte al administrador.";
            return;
        }

        if (AvailableRegisters.Count > 1 && SelectedRegister is null)
        {
            GeneralError = "Seleccione una caja.";
            return;
        }

        if (!TryParseAmount(OpeningAmountText, out var amount, out var parseError))
        {
            GeneralError = parseError;
            return;
        }

        IsBusy = true;

        try
        {
            var request = new OpenRegisterSessionRequest(SelectedRegister!.RegisterId, amount);
            var result = await _registerSessionService.OpenAsync(request, CancellationToken);

            if (result.Success)
            {
                RegisterOpened?.Invoke(this, result.ActiveSession!);
                return;
            }

            GeneralError = result.Status switch
            {
                RegisterSessionResultStatus.NotAuthenticated => "La sesión no está disponible. Inicie sesión nuevamente.",
                RegisterSessionResultStatus.NotAuthorized => "No tiene permiso para abrir la caja.",
                RegisterSessionResultStatus.InvalidInstallationState => "La instalación presenta una configuración inválida.",
                RegisterSessionResultStatus.RegisterNotFound => "La caja seleccionada no está disponible.",
                RegisterSessionResultStatus.RegisterInactive => "La caja seleccionada está inactiva.",
                RegisterSessionResultStatus.RegisterSelectionRequired => "Seleccione una caja.",
                RegisterSessionResultStatus.AlreadyOpen => "Ya existe una sesión abierta para esta caja.",
                RegisterSessionResultStatus.InvalidAmount => "El fondo inicial no es válido.",
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

    private Task ExecuteLogoutAsync()
    {
        _currentUserSession.Clear();
        LogoutRequested?.Invoke(this, EventArgs.Empty);

        return Task.CompletedTask;
    }

    private void HandleUnexpectedError(Exception exception)
    {
        LogUnexpectedError(_logger, exception);
        GeneralError = "Ocurrió un error inesperado. Intente nuevamente.";
        IsBusy = false;
    }

    private static bool TryParseAmount(string text, out decimal amount, out string? error)
    {
        amount = 0m;
        error = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            error = "El fondo inicial es obligatorio.";
            return false;
        }

        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out amount))
        {
            error = "El fondo inicial no es un monto válido.";
            return false;
        }

        if (amount < 0m)
        {
            error = "El fondo inicial no puede ser negativo.";
            return false;
        }

        if (amount > MaxOpeningAmount)
        {
            error = $"El fondo inicial no puede superar {MaxOpeningAmount.ToString("N2", CultureInfo.CurrentCulture)}.";
            return false;
        }

        amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        return true;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Error inesperado al abrir la caja.")]
    private static partial void LogUnexpectedError(ILogger logger, Exception exception);
}
