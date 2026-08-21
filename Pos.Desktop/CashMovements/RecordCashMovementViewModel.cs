using System.Globalization;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Pos.Application.CashMovements;
using Pos.Desktop.Common;
using Pos.Domain.CashMovements;

namespace Pos.Desktop.CashMovements;

// Diálogo único reutilizado tanto para "Entrada de efectivo" como "Salida de efectivo" (sección 20
// de la tarea: dos acciones explícitas, nunca un monto con signo). El shell (App.xaml.cs) llama a
// Load(type) justo después de resolver la ventana desde DI y antes de ShowDialog, igual patrón que
// AdjustInventoryWindow.Load.
public sealed partial class RecordCashMovementViewModel : ViewModelBase
{
    private const decimal MaxAmount = 999_999_999.99m;

    private readonly ICashMovementService _cashMovementService;
    private readonly ILogger<RecordCashMovementViewModel> _logger;
    private readonly AsyncRelayCommand _confirmCommand;
    private readonly AsyncRelayCommand _cancelCommand;

    private CashMovementType _type = CashMovementType.CashIn;
    private string _amountText = string.Empty;
    private string _reasonText = string.Empty;
    private bool _isBusy;
    private string? _generalError;

    public RecordCashMovementViewModel(ICashMovementService cashMovementService, ILogger<RecordCashMovementViewModel> logger)
    {
        _cashMovementService = cashMovementService ?? throw new ArgumentNullException(nameof(cashMovementService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _confirmCommand = new AsyncRelayCommand(ExecuteConfirmAsync, () => !IsBusy, HandleUnexpectedError);
        _cancelCommand = new AsyncRelayCommand(ExecuteCancelAsync, () => !IsBusy);
    }

    public event EventHandler<CashMovementEntry>? Confirmed;

    public event EventHandler? CancelRequested;

    public CancellationToken CancellationToken { get; set; }

    public ICommand ConfirmCommand => _confirmCommand;

    public ICommand CancelCommand => _cancelCommand;

    public string Title => _type == CashMovementType.CashIn ? "Entrada de efectivo" : "Salida de efectivo";

    public string ConfirmButtonText => _type == CashMovementType.CashIn ? "Registrar entrada" : "Registrar salida";

    public string AmountText
    {
        get => _amountText;
        set => SetProperty(ref _amountText, value);
    }

    public string ReasonText
    {
        get => _reasonText;
        set => SetProperty(ref _reasonText, value);
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

    public void Load(CashMovementType type)
    {
        _type = type;
        AmountText = string.Empty;
        ReasonText = string.Empty;
        GeneralError = null;

        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(ConfirmButtonText));
    }

    private async Task ExecuteConfirmAsync()
    {
        GeneralError = null;

        if (!decimal.TryParse(AmountText, NumberStyles.Number, CultureInfo.CurrentCulture, out var amount))
        {
            GeneralError = "El monto no es un valor válido.";
            return;
        }

        if (amount <= 0m)
        {
            GeneralError = "El monto debe ser mayor que cero.";
            return;
        }

        if (amount > MaxAmount)
        {
            GeneralError = $"El monto no puede superar {MaxAmount.ToString("N2", CultureInfo.CurrentCulture)}.";
            return;
        }

        if (string.IsNullOrWhiteSpace(ReasonText))
        {
            GeneralError = "El motivo es obligatorio.";
            return;
        }

        amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);

        IsBusy = true;

        try
        {
            var request = new RecordCashMovementRequest(amount, ReasonText);
            var result = _type == CashMovementType.CashIn
                ? await _cashMovementService.RecordCashInAsync(request, CancellationToken)
                : await _cashMovementService.RecordCashOutAsync(request, CancellationToken);

            if (result.Success)
            {
                Confirmed?.Invoke(this, result.Movement!);
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

    private static string ToErrorMessage(CashMovementResultStatus status) => status switch
    {
        CashMovementResultStatus.NotAuthenticated => "La sesión no está disponible. Inicie sesión nuevamente.",
        CashMovementResultStatus.NotAuthorized => "No tiene permiso para registrar movimientos de caja.",
        CashMovementResultStatus.InstallationRestricted => "La operación está restringida. Consulte al administrador.",
        CashMovementResultStatus.SessionNotFound => "No hay una caja abierta.",
        CashMovementResultStatus.SessionAlreadyClosed => "La caja ya fue cerrada.",
        CashMovementResultStatus.SessionBelongsToAnotherOrganization => "La caja no pertenece a esta instalación.",
        CashMovementResultStatus.InvalidAmount => "El monto no es válido.",
        CashMovementResultStatus.ReasonRequired => "El motivo es obligatorio.",
        CashMovementResultStatus.InsufficientExpectedCash => "El monto supera el efectivo esperado en caja.",
        _ => "Ocurrió un error inesperado. Intente nuevamente.",
    };

    [LoggerMessage(Level = LogLevel.Error, Message = "Error inesperado al registrar el movimiento de caja.")]
    private static partial void LogUnexpectedError(ILogger logger, Exception exception);
}
