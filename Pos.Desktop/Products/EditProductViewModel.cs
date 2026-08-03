using System.Globalization;
using System.Linq;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Pos.Application.Products.ManageProduct;
using Pos.Application.SalesCart;
using Pos.Desktop.Common;
using Pos.Domain.Common.Identifiers;

namespace Pos.Desktop.Products;

public sealed partial class EditProductViewModel : ViewModelBase
{
    private readonly IProductManagementService _productManagementService;
    private readonly ICurrentSalesCart _currentSalesCart;
    private readonly ILogger<EditProductViewModel> _logger;
    private readonly AsyncRelayCommand _saveCommand;
    private readonly AsyncRelayCommand _toggleActiveCommand;
    private readonly AsyncRelayCommand _adjustInventoryCommand;
    private readonly AsyncRelayCommand _cancelCommand;

    private ProductId _productId;
    private bool _isLoaded;
    private string _loadedSku = string.Empty;
    private string _sku = string.Empty;
    private string _barcode = string.Empty;
    private string _name = string.Empty;
    private string _description = string.Empty;
    private string _salePriceText = string.Empty;
    private string _costText = string.Empty;
    private bool _tracksInventory;
    private decimal _currentQuantity;
    private string _reorderPointText = string.Empty;
    private bool _isActive;
    private bool _isBusy;
    private string? _generalError;

    public EditProductViewModel(
        IProductManagementService productManagementService,
        ICurrentSalesCart currentSalesCart,
        ILogger<EditProductViewModel> logger)
    {
        _productManagementService = productManagementService ?? throw new ArgumentNullException(nameof(productManagementService));
        _currentSalesCart = currentSalesCart ?? throw new ArgumentNullException(nameof(currentSalesCart));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _saveCommand = new AsyncRelayCommand(ExecuteSaveAsync, () => !IsBusy && _isLoaded, HandleUnexpectedError);
        _toggleActiveCommand = new AsyncRelayCommand(ExecuteToggleActiveAsync, () => !IsBusy && _isLoaded, HandleUnexpectedError);
        _adjustInventoryCommand = new AsyncRelayCommand(
            ExecuteAdjustInventoryAsync, () => !IsBusy && _isLoaded && TracksInventory, HandleUnexpectedError);
        _cancelCommand = new AsyncRelayCommand(ExecuteCancelAsync, () => !IsBusy);
    }

    // Guardar cambios exitoso: el código detrás cierra la ventana con DialogResult=true.
    public event EventHandler? Saved;

    public event EventHandler? CancelRequested;

    // El ViewModel nunca abre ventanas: solo pide abrir AdjustInventoryWindow. El código detrás
    // de EditProductWindow reenvía el evento hasta App.xaml.cs, igual que NewProductRequested.
    public event EventHandler? AdjustInventoryRequested;

    // El ViewModel nunca muestra MessageBox: solo pide mostrar la advertencia (mismo patrón que
    // CancelSaleConfirmationRequested en MainWindowViewModel). Se dispara cuando el SKU cambió y el
    // producto sigue en el carrito actual (TAREA 24C, sección 15).
    public event EventHandler? CartWarningRequested;

    public CancellationToken CancellationToken { get; set; }

    public ICommand SaveCommand => _saveCommand;

    public ICommand ToggleActiveCommand => _toggleActiveCommand;

    public ICommand AdjustInventoryCommand => _adjustInventoryCommand;

    public ICommand CancelCommand => _cancelCommand;

    public ProductId ProductId => _productId;

    // SKU editable desde TAREA 24C (Product.ChangeSku ya existía en Domain sin usar). ProductId
    // nunca cambia; ventas históricas y líneas de carrito existentes conservan su snapshot.
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

    // TracksInventory: read-only. Cambiarlo implicaría crear/eliminar InventoryItem o alterar
    // semántica histórica; queda fuera de alcance de esta fase (ver inspección de TAREA 24B).
    public bool TracksInventory
    {
        get => _tracksInventory;
        private set
        {
            if (SetProperty(ref _tracksInventory, value))
            {
                OnPropertyChanged(nameof(CurrentQuantityText));
                _adjustInventoryCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public decimal CurrentQuantity
    {
        get => _currentQuantity;
        private set
        {
            if (SetProperty(ref _currentQuantity, value))
            {
                OnPropertyChanged(nameof(CurrentQuantityText));
            }
        }
    }

    public string CurrentQuantityText => TracksInventory
        ? CurrentQuantity.ToString(CultureInfo.CurrentCulture)
        : "No controla inventario";

    public string ReorderPointText
    {
        get => _reorderPointText;
        set => SetProperty(ref _reorderPointText, value);
    }

    public bool IsActive
    {
        get => _isActive;
        private set
        {
            if (SetProperty(ref _isActive, value))
            {
                OnPropertyChanged(nameof(ToggleActiveButtonText));
                OnPropertyChanged(nameof(IsActiveText));
            }
        }
    }

    public string ToggleActiveButtonText => IsActive ? "Desactivar" : "Activar";

    public string IsActiveText => IsActive ? "Activo" : "Inactivo";

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
                _saveCommand.RaiseCanExecuteChanged();
                _toggleActiveCommand.RaiseCanExecuteChanged();
                _adjustInventoryCommand.RaiseCanExecuteChanged();
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

    public async Task LoadAsync(ProductId productId)
    {
        IsBusy = true;

        try
        {
            var details = await _productManagementService.GetByIdAsync(productId, CancellationToken);

            if (details is null)
            {
                GeneralError = "No fue posible cargar el producto.";
                return;
            }

            ApplyDetails(details);
            _isLoaded = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    // Llamado desde el código detrás tras confirmar el ajuste en AdjustInventoryWindow: solo
    // refleja la nueva existencia ya persistida, no vuelve a llamar al servicio.
    public void ApplyInventoryAdjusted(decimal newQuantity) => CurrentQuantity = newQuantity;

    private void ApplyDetails(ProductDetails details)
    {
        _productId = details.ProductId;
        _loadedSku = details.Sku;
        Sku = details.Sku;
        Barcode = details.Barcode ?? string.Empty;
        Name = details.Name;
        Description = details.Description ?? string.Empty;
        SalePriceText = details.SalePriceAmount.ToString("F2", CultureInfo.CurrentCulture);
        CostText = details.CostAmount?.ToString("F2", CultureInfo.CurrentCulture) ?? string.Empty;
        TracksInventory = details.TracksInventory;
        CurrentQuantity = details.CurrentQuantity;
        ReorderPointText = details.TracksInventory
            ? details.ReorderPoint.ToString(CultureInfo.CurrentCulture)
            : string.Empty;
        IsActive = details.IsActive;
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

        decimal? reorderPoint = null;

        if (TracksInventory)
        {
            if (!TryParseNonNegative(
                    ReorderPointText,
                    "El stock mínimo es obligatorio.",
                    "El stock mínimo no es un valor válido.",
                    "El stock mínimo no puede ser negativo.",
                    out var reorderPointValue, out var reorderPointError))
            {
                GeneralError = reorderPointError;
                return;
            }

            reorderPoint = reorderPointValue;
        }

        var trimmedBarcode = Barcode.Trim();
        var barcode = string.IsNullOrWhiteSpace(trimmedBarcode) ? null : trimmedBarcode;

        var trimmedDescription = Description.Trim();
        var description = string.IsNullOrWhiteSpace(trimmedDescription) ? null : trimmedDescription;

        IsBusy = true;

        try
        {
            var request = new UpdateProductRequest(
                _productId, trimmedSku, barcode, trimmedName, description, salePrice, cost, reorderPoint);

            var result = await _productManagementService.UpdateAsync(request, CancellationToken);

            if (result.Success)
            {
                var previousSku = _loadedSku;
                ApplyDetails(result.Product!);

                var skuChanged = !string.Equals(previousSku, result.Product!.Sku, StringComparison.Ordinal);
                var isInCurrentCart = _currentSalesCart.Snapshot.Lines.Any(line => line.ProductId == _productId);

                if (skuChanged && isInCurrentCart)
                {
                    CartWarningRequested?.Invoke(this, EventArgs.Empty);
                }

                Saved?.Invoke(this, EventArgs.Empty);
                return;
            }

            GeneralError = ToUpdateErrorMessage(result.Status);
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

    private async Task ExecuteToggleActiveAsync()
    {
        GeneralError = null;
        IsBusy = true;

        try
        {
            var result = await _productManagementService.SetActiveAsync(_productId, !IsActive, CancellationToken);

            if (result.Success)
            {
                ApplyDetails(result.Product!);
                return;
            }

            GeneralError = ToUpdateErrorMessage(result.Status);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task ExecuteAdjustInventoryAsync()
    {
        AdjustInventoryRequested?.Invoke(this, EventArgs.Empty);

        return Task.CompletedTask;
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

    private static string ToUpdateErrorMessage(UpdateProductResultStatus status) => status switch
    {
        UpdateProductResultStatus.NotAuthenticated => "La sesión no está disponible. Inicie sesión nuevamente.",
        UpdateProductResultStatus.NotAuthorized => "No tiene permiso para editar productos.",
        UpdateProductResultStatus.ProductNotFound => "El producto ya no existe.",
        UpdateProductResultStatus.InvalidName => "El nombre no es válido.",
        UpdateProductResultStatus.InvalidSku => "El SKU no es válido.",
        UpdateProductResultStatus.InvalidBarcode => "El código de barras no es válido.",
        UpdateProductResultStatus.InvalidSalePrice => "El precio de venta no es válido.",
        UpdateProductResultStatus.InvalidCost => "El costo no es válido.",
        UpdateProductResultStatus.InvalidReorderPoint => "El stock mínimo no es válido.",
        UpdateProductResultStatus.DuplicateSku => "Ya existe otro producto con ese SKU.",
        UpdateProductResultStatus.DuplicateBarcode => "Ya existe otro producto con ese código de barras.",
        _ => "Ocurrió un error inesperado. Intente nuevamente.",
    };

    [LoggerMessage(Level = LogLevel.Error, Message = "Error inesperado al editar el producto.")]
    private static partial void LogUnexpectedError(ILogger logger, Exception exception);
}
