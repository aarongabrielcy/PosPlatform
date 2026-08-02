using System.Globalization;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Pos.Application.Sales.Checkout;
using Pos.Application.SalesCart;
using Pos.Desktop.Common;

namespace Pos.Desktop.Sales.Checkout;

// TAREA 25A: presenta el resumen de la venta actual (leído directamente de ICurrentSalesCart, la
// misma fuente que SalesView) y pide el efectivo recibido. Todo el checkout real (revalidación de
// carrito, creación de Sale/Payment, descuento de inventario, Commit) vive en ICheckoutService:
// este ViewModel nunca crea Sale ni toca repositorios/DbContext directamente.
public sealed partial class CheckoutViewModel : ViewModelBase
{
    private const decimal MaxCashTendered = 999_999_999.99m;

    private readonly ICheckoutService _checkoutService;
    private readonly ICurrentSalesCart _currentSalesCart;
    private readonly ILogger<CheckoutViewModel> _logger;
    private readonly AsyncRelayCommand _confirmCommand;
    private readonly AsyncRelayCommand _cancelCommand;

    private string _cashTenderedText = string.Empty;
    private bool _isBusy;
    private string? _generalError;

    public CheckoutViewModel(
        ICheckoutService checkoutService,
        ICurrentSalesCart currentSalesCart,
        ILogger<CheckoutViewModel> logger)
    {
        _checkoutService = checkoutService ?? throw new ArgumentNullException(nameof(checkoutService));
        _currentSalesCart = currentSalesCart ?? throw new ArgumentNullException(nameof(currentSalesCart));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _confirmCommand = new AsyncRelayCommand(ExecuteConfirmAsync, () => !IsBusy, HandleUnexpectedError);
        _cancelCommand = new AsyncRelayCommand(ExecuteCancelAsync, () => !IsBusy);
    }

    public event EventHandler<CheckoutSummary>? CheckoutCompleted;

    public event EventHandler? CancelRequested;

    public CancellationToken CancellationToken { get; set; }

    public ICommand ConfirmCommand => _confirmCommand;

    public ICommand CancelCommand => _cancelCommand;

    public string Currency => _currentSalesCart.Snapshot.Currency;

    public string TotalAmountText => FormatAmount(_currentSalesCart.Snapshot.TotalAmount);

    public string CashTenderedText
    {
        get => _cashTenderedText;
        set
        {
            if (SetProperty(ref _cashTenderedText, value))
            {
                OnPropertyChanged(nameof(ChangeText));
            }
        }
    }

    public string ChangeText
    {
        get
        {
            if (!TryParseCash(CashTenderedText, out var cashTendered, out _))
            {
                return string.Empty;
            }

            return FormatAmount(cashTendered - _currentSalesCart.Snapshot.TotalAmount);
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

        if (!TryParseCash(CashTenderedText, out var cashTendered, out var parseError))
        {
            GeneralError = parseError;
            return;
        }

        IsBusy = true;

        try
        {
            var result = await _checkoutService.CheckoutAsync(new CheckoutRequest(cashTendered), CancellationToken);

            if (result.Success)
            {
                CheckoutCompleted?.Invoke(this, result.Summary!);
                return;
            }

            GeneralError = ToErrorMessage(result);
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

    private bool TryParseCash(string text, out decimal amount, out string? error)
    {
        amount = 0m;
        error = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            error = "El efectivo recibido es obligatorio.";
            return false;
        }

        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out amount))
        {
            error = "El efectivo recibido no es un valor válido.";
            return false;
        }

        if (amount <= 0m)
        {
            error = "El efectivo recibido debe ser mayor que cero.";
            return false;
        }

        if (amount > MaxCashTendered)
        {
            error = $"El efectivo recibido no puede superar {MaxCashTendered.ToString("N2", CultureInfo.CurrentCulture)}.";
            return false;
        }

        amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);

        if (amount < _currentSalesCart.Snapshot.TotalAmount)
        {
            error = "El efectivo recibido es menor que el total.";
            return false;
        }

        return true;
    }

    private string FormatAmount(decimal amount) =>
        $"{amount.ToString("N2", CultureInfo.CurrentCulture)} {Currency}";

    private static string ToErrorMessage(CheckoutResult result) => result.Status switch
    {
        CheckoutResultStatus.NotAuthenticated => "Debes iniciar sesión para continuar.",
        CheckoutResultStatus.NotAuthorized => "No tienes permiso para procesar ventas.",
        CheckoutResultStatus.RegisterSessionRequired => "Debes abrir la caja para continuar.",
        CheckoutResultStatus.EmptyCart => "El carrito está vacío.",
        CheckoutResultStatus.CurrencyMismatch => "La moneda del carrito no coincide con la de la caja.",
        CheckoutResultStatus.ProductNotFound => "Un producto del carrito ya no existe o no pertenece a esta organización.",
        CheckoutResultStatus.ProductInactive => "Un producto del carrito ya no está activo.",
        CheckoutResultStatus.ProductChanged =>
            "El producto fue modificado desde que se agregó a la venta. Actualiza el producto en el carrito antes de cobrar.",
        CheckoutResultStatus.InsufficientStock => result.AvailableQuantity is { } quantity
            ? $"Existencia insuficiente. Disponible: {quantity.ToString(CultureInfo.CurrentCulture)}."
            : "No hay existencia suficiente para completar la venta.",
        CheckoutResultStatus.InvalidPayment => "El efectivo recibido no es válido.",
        CheckoutResultStatus.InsufficientCash => "El efectivo recibido es menor que el total.",
        CheckoutResultStatus.InternalValidationError => "El total de la venta no coincide con el carrito. Intenta nuevamente.",
        _ => "No fue posible completar el cobro.",
    };

    [LoggerMessage(Level = LogLevel.Error, Message = "Error inesperado al procesar el cobro.")]
    private static partial void LogUnexpectedError(ILogger logger, Exception exception);
}
