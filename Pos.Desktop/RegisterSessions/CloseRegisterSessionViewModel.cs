using System.Globalization;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Pos.Application.RegisterSessions;
using Pos.Desktop.Common;

namespace Pos.Desktop.RegisterSessions;

public sealed partial class CloseRegisterSessionViewModel : ViewModelBase
{
    private const decimal MaxClosingAmount = 999_999_999.99m;

    private readonly IRegisterSessionService _registerSessionService;
    private readonly ICurrentRegisterSession _currentRegisterSession;
    private readonly ILogger<CloseRegisterSessionViewModel> _logger;
    private readonly AsyncRelayCommand _confirmCommand;
    private readonly AsyncRelayCommand _cancelCommand;

    private string _closingAmountText = string.Empty;
    private bool _isBusy;
    private string? _generalError;

    public CloseRegisterSessionViewModel(
        IRegisterSessionService registerSessionService,
        ICurrentRegisterSession currentRegisterSession,
        ILogger<CloseRegisterSessionViewModel> logger)
    {
        _registerSessionService = registerSessionService ?? throw new ArgumentNullException(nameof(registerSessionService));
        _currentRegisterSession = currentRegisterSession ?? throw new ArgumentNullException(nameof(currentRegisterSession));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _confirmCommand = new AsyncRelayCommand(ExecuteConfirmAsync, () => !IsBusy, HandleUnexpectedError);
        _cancelCommand = new AsyncRelayCommand(ExecuteCancelAsync, () => !IsBusy);
    }

    public event EventHandler<RegisterSessionSummary>? RegisterClosed;

    public event EventHandler? CancelRequested;

    public CancellationToken CancellationToken { get; set; }

    public ICommand ConfirmCommand => _confirmCommand;

    public ICommand CancelCommand => _cancelCommand;

    public string RegisterName => _currentRegisterSession.Current?.RegisterName ?? string.Empty;

    public string OpenedByDisplayName => _currentRegisterSession.Current?.OpenedByDisplayName ?? string.Empty;

    public string OpenedAtText => _currentRegisterSession.Current is { } session
        ? session.OpenedAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
        : string.Empty;

    public string Currency => _currentRegisterSession.Current?.Currency ?? string.Empty;

    // Sin ventas ni movimientos en esta fase, el monto esperado siempre coincide con el fondo
    // inicial (ver RegisterSessionService.CloseAsync, que aplica la misma regla en Domain).
    public string OpeningAmountText => FormatAmount(_currentRegisterSession.Current?.OpeningAmount);

    public string ExpectedAmountText => FormatAmount(_currentRegisterSession.Current?.OpeningAmount);

    public string ClosingAmountText
    {
        get => _closingAmountText;
        set
        {
            if (SetProperty(ref _closingAmountText, value))
            {
                OnPropertyChanged(nameof(DifferenceText));
            }
        }
    }

    public string DifferenceText
    {
        get
        {
            if (_currentRegisterSession.Current is not { } current)
            {
                return string.Empty;
            }

            if (!TryParseAmount(ClosingAmountText, out var closingAmount, out _))
            {
                return string.Empty;
            }

            var difference = closingAmount - current.OpeningAmount;

            return $"{difference.ToString("N2", CultureInfo.CurrentCulture)} {current.Currency}";
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
                _confirmCommand.RaiseCanExecuteChanged();
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

    private async Task ExecuteConfirmAsync()
    {
        GeneralError = null;

        if (!TryParseAmount(ClosingAmountText, out var amount, out var parseError))
        {
            GeneralError = parseError;
            return;
        }

        IsBusy = true;

        try
        {
            var request = new CloseRegisterSessionRequest(amount);
            var result = await _registerSessionService.CloseAsync(request, CancellationToken);

            if (result.Success)
            {
                RegisterClosed?.Invoke(this, result.Summary!);
                return;
            }

            GeneralError = result.Status switch
            {
                RegisterSessionResultStatus.NotAuthenticated => "La sesión no está disponible. Inicie sesión nuevamente.",
                RegisterSessionResultStatus.NotAuthorized => "No tiene permiso para cerrar la caja.",
                RegisterSessionResultStatus.SessionNotFound => "No se encontró una caja abierta para cerrar.",
                RegisterSessionResultStatus.SessionAlreadyClosed => "La caja ya fue cerrada.",
                RegisterSessionResultStatus.SessionBelongsToAnotherOrganization => "La caja no pertenece a esta instalación.",
                RegisterSessionResultStatus.InvalidAmount => "El monto contado no es válido.",
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

    private static bool TryParseAmount(string text, out decimal amount, out string? error)
    {
        amount = 0m;
        error = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            error = "El monto contado es obligatorio.";
            return false;
        }

        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out amount))
        {
            error = "El monto contado no es un valor válido.";
            return false;
        }

        if (amount < 0m)
        {
            error = "El monto contado no puede ser negativo.";
            return false;
        }

        if (amount > MaxClosingAmount)
        {
            error = $"El monto contado no puede superar {MaxClosingAmount.ToString("N2", CultureInfo.CurrentCulture)}.";
            return false;
        }

        amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        return true;
    }

    private static string FormatAmount(decimal? amount) =>
        amount is { } value ? value.ToString("N2", CultureInfo.CurrentCulture) : string.Empty;

    [LoggerMessage(Level = LogLevel.Error, Message = "Error inesperado al cerrar la caja.")]
    private static partial void LogUnexpectedError(ILogger logger, Exception exception);
}
