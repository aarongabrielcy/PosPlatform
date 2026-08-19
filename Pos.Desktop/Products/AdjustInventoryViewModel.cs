using System.Globalization;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Pos.Application.Products.ManageProduct;
using Pos.Desktop.Common;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Inventory;

namespace Pos.Desktop.Products;

// TAREA 25A-FIX defecto 2: el campo de entrada es la existencia FINAL deseada ("Nueva
// existencia"), no una magnitud de ajuste con dirección. El usuario ya no necesita calcular
// mentalmente cuánto disminuir para llegar a una existencia objetivo (p. ej. 2 -> 0): el
// ViewModel calcula la diferencia y decide Increase/Decrease antes de llamar al servicio, que
// sigue recibiendo exactamente lo mismo que antes (AdjustmentType + magnitud positiva).
public sealed partial class AdjustInventoryViewModel : ViewModelBase
{
    private readonly IProductManagementService _productManagementService;
    private readonly ILogger<AdjustInventoryViewModel> _logger;
    private readonly AsyncRelayCommand _confirmCommand;
    private readonly AsyncRelayCommand _cancelCommand;

    private ProductId _productId;
    private string _productName = string.Empty;
    private decimal _currentQuantity;
    private string _newQuantityText = string.Empty;
    private bool _isBusy;
    private string? _generalError;
    private decimal? _confirmedNewQuantity;

    public AdjustInventoryViewModel(
        IProductManagementService productManagementService,
        ILogger<AdjustInventoryViewModel> logger)
    {
        _productManagementService = productManagementService ?? throw new ArgumentNullException(nameof(productManagementService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _confirmCommand = new AsyncRelayCommand(ExecuteConfirmAsync, () => !IsBusy, HandleUnexpectedError);
        _cancelCommand = new AsyncRelayCommand(ExecuteCancelAsync, () => !IsBusy);
    }

    public event EventHandler? Confirmed;

    public event EventHandler? CancelRequested;

    public CancellationToken CancellationToken { get; set; }

    public ICommand ConfirmCommand => _confirmCommand;

    public ICommand CancelCommand => _cancelCommand;

    public string ProductName
    {
        get => _productName;
        private set => SetProperty(ref _productName, value);
    }

    public decimal CurrentQuantity
    {
        get => _currentQuantity;
        private set
        {
            if (SetProperty(ref _currentQuantity, value))
            {
                OnPropertyChanged(nameof(DifferenceText));
            }
        }
    }

    public string NewQuantityText
    {
        get => _newQuantityText;
        set
        {
            if (SetProperty(ref _newQuantityText, value))
            {
                OnPropertyChanged(nameof(DifferenceText));
            }
        }
    }

    // Solo vista previa: no valida ni persiste nada, ConfirmCommand vuelve a validar por completo.
    public string DifferenceText
    {
        get
        {
            if (!decimal.TryParse(NewQuantityText, NumberStyles.Number, CultureInfo.CurrentCulture, out var newQuantity))
            {
                return "—";
            }

            var difference = newQuantity - CurrentQuantity;

            return difference > 0
                ? $"+{difference.ToString(CultureInfo.CurrentCulture)}"
                : difference.ToString(CultureInfo.CurrentCulture);
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

    public decimal? ConfirmedNewQuantity => _confirmedNewQuantity;

    // El producto y la existencia actual ya son conocidos por EditProductViewModel (que acaba de
    // cargarlos): no se vuelve a consultar el servicio solo para mostrarlos aquí.
    public void Load(ProductId productId, string productName, decimal currentQuantity)
    {
        _productId = productId;
        ProductName = productName;
        CurrentQuantity = currentQuantity;
    }

    private async Task ExecuteConfirmAsync()
    {
        GeneralError = null;

        if (!decimal.TryParse(NewQuantityText, NumberStyles.Number, CultureInfo.CurrentCulture, out var newQuantity))
        {
            GeneralError = "La nueva existencia debe ser un número.";
            return;
        }

        if (newQuantity < 0m)
        {
            GeneralError = "La nueva existencia no puede ser negativa.";
            return;
        }

        var difference = newQuantity - CurrentQuantity;

        if (difference == 0m)
        {
            GeneralError = "La nueva existencia debe ser diferente a la actual.";
            return;
        }

        var adjustmentType = difference > 0m ? InventoryAdjustmentType.Increase : InventoryAdjustmentType.Decrease;
        var magnitude = Math.Abs(difference);

        IsBusy = true;

        try
        {
            var request = new AdjustProductInventoryRequest(_productId, adjustmentType, magnitude);
            var result = await _productManagementService.AdjustInventoryAsync(request, CancellationToken);

            if (result.Success)
            {
                _confirmedNewQuantity = result.NewQuantity;
                Confirmed?.Invoke(this, EventArgs.Empty);
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

    private static string ToErrorMessage(AdjustProductInventoryResultStatus status) => status switch
    {
        AdjustProductInventoryResultStatus.NotAuthenticated => "La sesión no está disponible. Inicie sesión nuevamente.",
        AdjustProductInventoryResultStatus.NotAuthorized => "No tiene permiso para ajustar inventario.",
        AdjustProductInventoryResultStatus.InstallationRestricted => "La operación está restringida. Consulte al administrador.",
        AdjustProductInventoryResultStatus.RegisterSessionRequired => "Debe haber una caja abierta para ajustar inventario.",
        AdjustProductInventoryResultStatus.ProductNotFound => "El producto ya no existe.",
        AdjustProductInventoryResultStatus.ProductDoesNotTrackInventory => "El producto no controla inventario.",
        AdjustProductInventoryResultStatus.InventoryItemNotFound => "No existe inventario para este producto en la sucursal actual.",
        AdjustProductInventoryResultStatus.InvalidQuantity => "La cantidad debe ser mayor que cero.",
        AdjustProductInventoryResultStatus.ResultingQuantityNegative => "La existencia resultante no puede ser negativa.",
        _ => "Ocurrió un error inesperado. Intente nuevamente.",
    };

    [LoggerMessage(Level = LogLevel.Error, Message = "Error inesperado al ajustar el inventario.")]
    private static partial void LogUnexpectedError(ILogger logger, Exception exception);
}
