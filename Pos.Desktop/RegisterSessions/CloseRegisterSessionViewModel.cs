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
    private RegisterClosingSummary? _summary;

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

    // OpeningAmountText/CashSalesAmountText/CardSalesAmountText/GrossSalesAmountText/
    // ExpectedAmountText provienen del summary cargado por LoadAsync
    // (RegisterSessionService.GetClosingSummaryAsync), nunca del OpeningAmount en caché de
    // ICurrentRegisterSession: ese valor nunca refleja las ventas realizadas durante la sesión
    // (TAREA 25A-FIX, defecto 1). ExpectedAmount excluye ventas Card (TAREA 25C): una venta con
    // tarjeta no es efectivo físico en el cajón.
    public string OpeningAmountText => FormatAmount(_summary?.OpeningFloat);

    public string CashSalesAmountText => FormatAmount(_summary?.CompletedCashSales);

    public string CardSalesAmountText => FormatAmount(_summary?.CompletedCardSales);

    public string GrossSalesAmountText => FormatAmount(_summary?.GrossSales);

    public string ExpectedAmountText => FormatAmount(_summary?.ExpectedCash);

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

    // Misma fórmula que RegisterSession.Close (Domain): CashDifference = CountedCash - ExpectedCash,
    // nunca CountedCash - OpeningFloat. Antes de que LoadAsync termine no hay summary y se muestra
    // vacío, igual que cuando el monto contado todavía no es un decimal válido.
    public string DifferenceText
    {
        get
        {
            if (_summary is not { } summary)
            {
                return string.Empty;
            }

            if (!TryParseAmount(ClosingAmountText, out var closingAmount, out _))
            {
                return string.Empty;
            }

            var difference = closingAmount - summary.ExpectedCash;

            return $"{difference.ToString("N2", CultureInfo.CurrentCulture)} {summary.Currency}";
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

    // Llamado desde el código detrás antes de mostrar la ventana (mismo patrón que
    // EditProductWindow.LoadAsync): el summary debe estar disponible desde el primer frame, nunca
    // mostrar $0.00/fondo inicial como placeholder de "sin ventas".
    public async Task LoadAsync()
    {
        GeneralError = null;
        IsBusy = true;

        try
        {
            var result = await _registerSessionService.GetClosingSummaryAsync(CancellationToken);

            if (result.Success)
            {
                _summary = result.Summary;
            }
            else
            {
                GeneralError = ToErrorMessage(result.Status);
            }
        }
        catch (OperationCanceledException)
        {
            // Ventana cerrada mientras la operación estaba en curso: no queda UI que actualizar.
        }
        finally
        {
            OnPropertyChanged(nameof(OpeningAmountText));
            OnPropertyChanged(nameof(CashSalesAmountText));
            OnPropertyChanged(nameof(CardSalesAmountText));
            OnPropertyChanged(nameof(GrossSalesAmountText));
            OnPropertyChanged(nameof(ExpectedAmountText));
            OnPropertyChanged(nameof(DifferenceText));
            IsBusy = false;
        }
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

            GeneralError = ToErrorMessage(result.Status);
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

    private static string ToErrorMessage(RegisterSessionResultStatus status) => status switch
    {
        RegisterSessionResultStatus.NotAuthenticated => "La sesión no está disponible. Inicie sesión nuevamente.",
        RegisterSessionResultStatus.NotAuthorized => "No tiene permiso para cerrar la caja.",
        RegisterSessionResultStatus.SessionNotFound => "No se encontró una caja abierta para cerrar.",
        RegisterSessionResultStatus.SessionAlreadyClosed => "La caja ya fue cerrada.",
        RegisterSessionResultStatus.SessionBelongsToAnotherOrganization => "La caja no pertenece a esta instalación.",
        RegisterSessionResultStatus.InvalidAmount => "El monto contado no es válido.",
        _ => "Ocurrió un error inesperado. Intente nuevamente.",
    };

    private static string FormatAmount(decimal? amount) =>
        amount is { } value ? value.ToString("N2", CultureInfo.CurrentCulture) : string.Empty;

    [LoggerMessage(Level = LogLevel.Error, Message = "Error inesperado al cerrar la caja.")]
    private static partial void LogUnexpectedError(ILogger logger, Exception exception);
}
