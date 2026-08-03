using System.Globalization;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Pos.Application.Products.CreateProduct;
using Pos.Desktop.Common;

namespace Pos.Desktop.Products;

public sealed partial class CreateProductViewModel : ViewModelBase
{
    private readonly ICreateProductService _createProductService;
    private readonly ILogger<CreateProductViewModel> _logger;
    private readonly AsyncRelayCommand _saveCommand;
    private readonly AsyncRelayCommand _cancelCommand;

    private string _sku = string.Empty;
    private string _barcode = string.Empty;
    private string _name = string.Empty;
    private string _description = string.Empty;
    private string _salePriceText = string.Empty;
    private string _costText = string.Empty;
    private bool _tracksInventory;
    private string _initialQuantityText = string.Empty;
    private string _reorderPointText = string.Empty;
    private bool _isBusy;
    private string? _generalError;

    public CreateProductViewModel(
        ICreateProductService createProductService,
        ILogger<CreateProductViewModel> logger)
    {
        _createProductService = createProductService ?? throw new ArgumentNullException(nameof(createProductService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _saveCommand = new AsyncRelayCommand(ExecuteSaveAsync, () => !IsBusy, HandleUnexpectedError);
        _cancelCommand = new AsyncRelayCommand(ExecuteCancelAsync, () => !IsBusy);
    }

    public event EventHandler<string>? ProductCreated;

    public event EventHandler? CancelRequested;

    public CancellationToken CancellationToken { get; set; }

    public ICommand SaveCommand => _saveCommand;

    public ICommand CancelCommand => _cancelCommand;

    public string Sku
    {
        get => _sku;
        set => SetProperty(ref _sku, value);
    }

    public string Barcode
    {
        get => _barcode;
        set => SetProperty(ref _barcode, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public string SalePriceText
    {
        get => _salePriceText;
        set => SetProperty(ref _salePriceText, value);
    }

    public string CostText
    {
        get => _costText;
        set => SetProperty(ref _costText, value);
    }

    public bool TracksInventory
    {
        get => _tracksInventory;
        set
        {
            if (SetProperty(ref _tracksInventory, value) && !value)
            {
                InitialQuantityText = string.Empty;
                ReorderPointText = string.Empty;
            }
        }
    }

    public string InitialQuantityText
    {
        get => _initialQuantityText;
        set => SetProperty(ref _initialQuantityText, value);
    }

    public string ReorderPointText
    {
        get => _reorderPointText;
        set => SetProperty(ref _reorderPointText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
                _saveCommand.RaiseCanExecuteChanged();
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

    private async Task ExecuteSaveAsync()
    {
        GeneralError = null;

        var trimmedSku = Sku.Trim();

        if (string.IsNullOrWhiteSpace(trimmedSku))
        {
            GeneralError = "El SKU es obligatorio.";
            return;
        }

        var trimmedName = Name.Trim();

        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            GeneralError = "El nombre es obligatorio.";
            return;
        }

        if (!TryParseNonNegative(
                SalePriceText,
                "El precio de venta es obligatorio.",
                "El precio de venta no es un valor válido.",
                "El precio de venta no puede ser negativo.",
                out var salePrice, out var salePriceError))
        {
            GeneralError = salePriceError;
            return;
        }

        decimal? cost = null;

        if (!string.IsNullOrWhiteSpace(CostText))
        {
            if (!TryParseNonNegative(
                    CostText,
                    "El costo es obligatorio.",
                    "El costo no es un valor válido.",
                    "El costo no puede ser negativo.",
                    out var costValue, out var costError))
            {
                GeneralError = costError;
                return;
            }

            cost = costValue;
        }

        var initialQuantity = 0m;
        var reorderPoint = 0m;

        if (TracksInventory)
        {
            if (!TryParseNonNegative(
                    InitialQuantityText,
                    "La existencia inicial es obligatoria.",
                    "La existencia inicial no es un valor válido.",
                    "La existencia inicial no puede ser negativa.",
                    out initialQuantity, out var quantityError))
            {
                GeneralError = quantityError;
                return;
            }

            if (!TryParseNonNegative(
                    ReorderPointText,
                    "El stock mínimo es obligatorio.",
                    "El stock mínimo no es un valor válido.",
                    "El stock mínimo no puede ser negativo.",
                    out reorderPoint, out var reorderError))
            {
                GeneralError = reorderError;
                return;
            }
        }

        var trimmedBarcode = Barcode.Trim();
        var barcode = string.IsNullOrWhiteSpace(trimmedBarcode) ? null : trimmedBarcode;

        var trimmedDescription = Description.Trim();
        var description = string.IsNullOrWhiteSpace(trimmedDescription) ? null : trimmedDescription;

        IsBusy = true;

        try
        {
            var request = new CreateProductRequest(
                trimmedSku, barcode, trimmedName, description, salePrice, cost, TracksInventory, initialQuantity, reorderPoint);

            var result = await _createProductService.CreateAsync(request, CancellationToken);

            if (result.Success)
            {
                ProductCreated?.Invoke(this, result.Sku!);
                return;
            }

            GeneralError = result.Status switch
            {
                CreateProductResultStatus.NotAuthenticated => "La sesión no está disponible. Inicie sesión nuevamente.",
                CreateProductResultStatus.NotAuthorized => "No tiene permiso para crear productos.",
                CreateProductResultStatus.RegisterSessionRequired => "Debe abrir la caja para crear productos.",
                CreateProductResultStatus.InvalidSku => "El SKU no es válido.",
                CreateProductResultStatus.InvalidName => "El nombre no es válido.",
                CreateProductResultStatus.InvalidBarcode => "El código de barras no es válido.",
                CreateProductResultStatus.InvalidSalePrice => "El precio de venta no es válido.",
                CreateProductResultStatus.InvalidCost => "El costo no es válido.",
                CreateProductResultStatus.InvalidInitialQuantity => "La existencia inicial no es válida.",
                CreateProductResultStatus.InvalidReorderPoint => "El stock mínimo no es válido.",
                CreateProductResultStatus.DuplicateSku => "Ya existe un producto con ese SKU.",
                CreateProductResultStatus.DuplicateBarcode => "Ya existe un producto con ese código de barras.",
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

    private static bool TryParseNonNegative(
        string text, string requiredMessage, string invalidMessage, string negativeMessage,
        out decimal amount, out string? error)
    {
        amount = 0m;
        error = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            error = requiredMessage;
            return false;
        }

        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out amount))
        {
            error = invalidMessage;
            return false;
        }

        if (amount < 0m)
        {
            error = negativeMessage;
            return false;
        }

        return true;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Error inesperado al crear el producto.")]
    private static partial void LogUnexpectedError(ILogger logger, Exception exception);
}
