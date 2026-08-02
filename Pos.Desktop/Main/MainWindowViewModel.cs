using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Pos.Application.Authentication;
using Pos.Application.Products.ManageProduct;
using Pos.Application.RegisterSessions;
using Pos.Application.SalesCart;
using Pos.Desktop.Common;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;

namespace Pos.Desktop.Main;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly ICurrentUserSession _session;
    private readonly ICurrentRegisterSession _registerSession;
    private readonly ISalesCartService _salesCartService;
    private readonly IProductManagementService _productManagementService;
    private readonly ICurrentSalesCart _currentSalesCart;
    private readonly AsyncRelayCommand _logoutCommand;
    private readonly AsyncRelayCommand _closeRegisterCommand;
    private readonly AsyncRelayCommand _searchCommand;
    private readonly AsyncRelayCommand _addSelectedProductCommand;
    private readonly AsyncRelayCommand<SalesCartLine> _increaseQuantityCommand;
    private readonly AsyncRelayCommand<SalesCartLine> _decreaseQuantityCommand;
    private readonly AsyncRelayCommand<SalesCartLine> _removeLineCommand;
    private readonly AsyncRelayCommand _cancelSaleCommand;
    private readonly AsyncRelayCommand _newProductCommand;
    private readonly AsyncRelayCommand _editProductCommand;

    private string? _logoutBlockedMessage;
    private string? _closeRegisterBlockedMessage;
    private string _searchText = string.Empty;
    private string? _searchStatusMessage;
    private ProductSearchResult? _selectedSearchResult;
    private bool _includeInactive;
    private string _subtotal = string.Empty;
    private string _discountTotal = string.Empty;
    private string _taxTotal = string.Empty;
    private string _grandTotal = string.Empty;
    private bool _isSearching;
    private bool _isBusy;
    private string? _generalError;
    private bool _hasItems;

    public MainWindowViewModel(
        ICurrentUserSession session,
        ICurrentRegisterSession registerSession,
        ISalesCartService salesCartService,
        IProductManagementService productManagementService,
        ICurrentSalesCart currentSalesCart)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _registerSession = registerSession ?? throw new ArgumentNullException(nameof(registerSession));
        _salesCartService = salesCartService ?? throw new ArgumentNullException(nameof(salesCartService));
        _productManagementService = productManagementService ?? throw new ArgumentNullException(nameof(productManagementService));
        _currentSalesCart = currentSalesCart ?? throw new ArgumentNullException(nameof(currentSalesCart));

        _logoutCommand = new AsyncRelayCommand(ExecuteLogoutAsync);
        _closeRegisterCommand = new AsyncRelayCommand(ExecuteCloseRegisterAsync);
        _searchCommand = new AsyncRelayCommand(ExecuteSearchAsync, onError: HandleUnexpectedError);
        _addSelectedProductCommand = new AsyncRelayCommand(
            ExecuteAddSelectedProductAsync, () => SelectedSearchResult is { IsAvailable: true }, HandleUnexpectedError);
        _increaseQuantityCommand = new AsyncRelayCommand<SalesCartLine>(ExecuteIncreaseQuantityAsync, onError: HandleUnexpectedError);
        _decreaseQuantityCommand = new AsyncRelayCommand<SalesCartLine>(
            ExecuteDecreaseQuantityAsync, line => line is not null && line.Quantity > 1m, HandleUnexpectedError);
        _removeLineCommand = new AsyncRelayCommand<SalesCartLine>(ExecuteRemoveLineAsync, onError: HandleUnexpectedError);
        _cancelSaleCommand = new AsyncRelayCommand(ExecuteCancelSaleAsync);
        _newProductCommand = new AsyncRelayCommand(ExecuteNewProductAsync);
        _editProductCommand = new AsyncRelayCommand(
            ExecuteEditProductAsync, () => SelectedSearchResult is not null && CanManageProducts && !IsBusy, HandleUnexpectedError);

        CartLines = new ObservableCollection<SalesCartLine>();
        SearchResults = new ObservableCollection<ProductSearchResult>();
        ApplySnapshot(_currentSalesCart.Snapshot);
    }

    public event EventHandler? LogoutRequested;

    public event EventHandler? CloseRegisterRequested;

    // El ViewModel nunca muestra MessageBox directamente; solo pide confirmación. El código
    // detrás de MainWindow decide cómo confirmarla y llama a ConfirmCancelSale().
    public event EventHandler? CancelSaleConfirmationRequested;

    // MainWindowViewModel nunca abre ventanas: solo pide abrir CreateProductWindow. El código
    // detrás de MainWindow reenvía el evento hasta App.xaml.cs, único lugar que resuelve
    // ventanas desde el contenedor de DI.
    public event EventHandler? NewProductRequested;

    // Igual patrón que NewProductRequested, pero para EditProductWindow: el ProductId del
    // producto seleccionado viaja en el evento para que App.xaml.cs pueda cargarlo antes de
    // mostrar la ventana.
    public event EventHandler<ProductId>? EditProductRequested;

    public ICommand LogoutCommand => _logoutCommand;

    public ICommand CloseRegisterCommand => _closeRegisterCommand;

    public ICommand SearchCommand => _searchCommand;

    public ICommand AddSelectedProductCommand => _addSelectedProductCommand;

    public ICommand IncreaseQuantityCommand => _increaseQuantityCommand;

    public ICommand DecreaseQuantityCommand => _decreaseQuantityCommand;

    public ICommand RemoveLineCommand => _removeLineCommand;

    public ICommand CancelSaleCommand => _cancelSaleCommand;

    public ICommand NewProductCommand => _newProductCommand;

    public ICommand EditProductCommand => _editProductCommand;

    // Sesión ausente produce un estado seguro: cadenas vacías en lugar de excepción.
    public string DisplayName => _session.CurrentUser?.DisplayName ?? string.Empty;

    public string RoleName => _session.CurrentUser?.RoleName ?? string.Empty;

    // Gobierna la visibilidad/habilitación del botón "Editar producto" y del checkbox "Incluir
    // inactivos": ambos son funcionalidad administrativa, no disponible para cualquier cajero.
    public bool CanManageProducts => _session.CurrentUser?.HasPermission(Permission.ManageProducts) ?? false;

    public bool IsRegisterOpen => _registerSession.IsOpen;

    public string RegisterName => _registerSession.Current?.RegisterName ?? string.Empty;

    public string RegisterOpenedAtText => _registerSession.Current is { } session
        ? session.OpenedAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
        : string.Empty;

    public string RegisterOpeningAmountText => _registerSession.Current is { } session
        ? $"{session.OpeningAmount.ToString("N2", CultureInfo.CurrentCulture)} {session.Currency}"
        : string.Empty;

    public string RegisterStatusText => IsRegisterOpen ? "Caja abierta" : string.Empty;

    public string? LogoutBlockedMessage
    {
        get => _logoutBlockedMessage;
        private set => SetProperty(ref _logoutBlockedMessage, value);
    }

    public string? CloseRegisterBlockedMessage
    {
        get => _closeRegisterBlockedMessage;
        private set => SetProperty(ref _closeRegisterBlockedMessage, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                // Un término nuevo invalida el mensaje del resultado anterior: evita mostrar
                // "No se encontraron productos" mientras el usuario ya está escribiendo otra cosa.
                SearchStatusMessage = null;
            }
        }
    }

    public string? SearchStatusMessage
    {
        get => _searchStatusMessage;
        private set => SetProperty(ref _searchStatusMessage, value);
    }

    // Solo tiene efecto para usuarios con ManageProducts: MainWindow.xaml la oculta/deshabilita
    // para el resto. La búsqueda del carrito (SalesCartService) nunca incluye inactivos.
    public bool IncludeInactive
    {
        get => _includeInactive;
        set
        {
            if (SetProperty(ref _includeInactive, value) && _searchCommand.CanExecute(null))
            {
                _searchCommand.Execute(null);
            }
        }
    }

    public ObservableCollection<ProductSearchResult> SearchResults { get; }

    public ProductSearchResult? SelectedSearchResult
    {
        get => _selectedSearchResult;
        set
        {
            if (SetProperty(ref _selectedSearchResult, value))
            {
                _addSelectedProductCommand.RaiseCanExecuteChanged();
                _editProductCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public ObservableCollection<SalesCartLine> CartLines { get; }

    public string Subtotal
    {
        get => _subtotal;
        private set => SetProperty(ref _subtotal, value);
    }

    public string DiscountTotal
    {
        get => _discountTotal;
        private set => SetProperty(ref _discountTotal, value);
    }

    public string TaxTotal
    {
        get => _taxTotal;
        private set => SetProperty(ref _taxTotal, value);
    }

    public string GrandTotal
    {
        get => _grandTotal;
        private set => SetProperty(ref _grandTotal, value);
    }

    public bool IsSearching
    {
        get => _isSearching;
        private set => SetProperty(ref _isSearching, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                _editProductCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string? GeneralError
    {
        get => _generalError;
        private set => SetProperty(ref _generalError, value);
    }

    public bool HasItems
    {
        get => _hasItems;
        private set => SetProperty(ref _hasItems, value);
    }

    // Llamado desde el código detrás de MainWindow tras confirmar el diálogo. Cancelar solo
    // limpia el carrito: no cierra caja ni cierra sesión.
    public void ConfirmCancelSale()
    {
        var result = _salesCartService.Clear();
        ApplyResult(result);
    }

    // Llamado desde App.xaml.cs tras cerrar CreateProductWindow con éxito: coloca el SKU recién
    // creado en el buscador y ejecuta la búsqueda para que el producto aparezca de inmediato. No
    // se agrega automáticamente al carrito.
    public void ApplyProductCreated(string sku)
    {
        SearchText = sku;

        if (_searchCommand.CanExecute(null))
        {
            _searchCommand.Execute(null);
        }
    }

    // Llamado desde App.xaml.cs tras cerrar EditProductWindow (edición, activar/desactivar o
    // ajuste de inventario): refresca el buscador con el SKU del producto editado, igual que
    // ApplyProductCreated. Si el producto quedó inactivo y no se incluyen inactivos, desaparece
    // del grid como se espera.
    public void ApplyProductUpdated(string sku) => ApplyProductCreated(sku);

    private Task ExecuteLogoutAsync()
    {
        if (_registerSession.IsOpen)
        {
            LogoutBlockedMessage = "Debes cerrar la caja antes de cerrar sesión.";
            return Task.CompletedTask;
        }

        LogoutBlockedMessage = null;
        _session.Clear();
        LogoutRequested?.Invoke(this, EventArgs.Empty);

        return Task.CompletedTask;
    }

    private Task ExecuteCloseRegisterAsync()
    {
        if (CartLines.Count > 0)
        {
            CloseRegisterBlockedMessage = "Cancela la venta actual antes de cerrar la caja.";
            return Task.CompletedTask;
        }

        CloseRegisterBlockedMessage = null;
        CloseRegisterRequested?.Invoke(this, EventArgs.Empty);

        return Task.CompletedTask;
    }

    private async Task ExecuteSearchAsync()
    {
        var trimmedSearchText = SearchText.Trim();

        if (trimmedSearchText.Length == 0)
        {
            SearchResults.Clear();
            SearchStatusMessage = "Escribe un SKU, código de barras o nombre para buscar.";
            GeneralError = null;

            return;
        }

        IsSearching = true;

        try
        {
            // "Incluir inactivos" solo tiene efecto para usuarios con ManageProducts; el resto
            // (y el propio carrito de venta) siempre buscan exclusivamente productos activos a
            // través de SalesCartService.
            var results = IncludeInactive && CanManageProducts
                ? await _productManagementService.SearchAsync(SearchText, includeInactive: true)
                : await _salesCartService.SearchProductsAsync(SearchText);

            SearchResults.Clear();

            foreach (var result in results)
            {
                SearchResults.Add(result);
            }

            SearchStatusMessage = results.Count switch
            {
                0 => "No se encontraron productos.",
                1 => "1 producto encontrado.",
                _ => $"{results.Count} productos encontrados.",
            };

            GeneralError = null;
        }
        finally
        {
            IsSearching = false;
        }
    }

    private async Task ExecuteAddSelectedProductAsync()
    {
        var selected = SelectedSearchResult;

        if (selected is null)
        {
            return;
        }

        IsBusy = true;

        try
        {
            var result = await _salesCartService.AddProductAsync(new AddProductToCartRequest(selected.ProductId));
            ApplyResult(result);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExecuteIncreaseQuantityAsync(SalesCartLine? line)
    {
        if (line is null)
        {
            return;
        }

        IsBusy = true;

        try
        {
            var result = await _salesCartService.UpdateQuantityAsync(
                new UpdateCartLineQuantityRequest(line.ProductId, line.Quantity + 1m));
            ApplyResult(result);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExecuteDecreaseQuantityAsync(SalesCartLine? line)
    {
        if (line is null || line.Quantity <= 1m)
        {
            return;
        }

        IsBusy = true;

        try
        {
            var result = await _salesCartService.UpdateQuantityAsync(
                new UpdateCartLineQuantityRequest(line.ProductId, line.Quantity - 1m));
            ApplyResult(result);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task ExecuteRemoveLineAsync(SalesCartLine? line)
    {
        if (line is null)
        {
            return Task.CompletedTask;
        }

        var result = _salesCartService.RemoveLine(line.ProductId);
        ApplyResult(result);

        return Task.CompletedTask;
    }

    private Task ExecuteCancelSaleAsync()
    {
        if (CartLines.Count == 0)
        {
            return Task.CompletedTask;
        }

        CancelSaleConfirmationRequested?.Invoke(this, EventArgs.Empty);

        return Task.CompletedTask;
    }

    private Task ExecuteNewProductAsync()
    {
        NewProductRequested?.Invoke(this, EventArgs.Empty);

        return Task.CompletedTask;
    }

    private Task ExecuteEditProductAsync()
    {
        if (SelectedSearchResult is { } selected)
        {
            EditProductRequested?.Invoke(this, selected.ProductId);
        }

        return Task.CompletedTask;
    }

    private void ApplyResult(SalesCartResult result)
    {
        if (result.Success && result.Snapshot is not null)
        {
            ApplySnapshot(result.Snapshot);
            GeneralError = null;
        }
        else
        {
            GeneralError = ToErrorMessage(result.Status, result.AvailableQuantity);
        }
    }

    private void ApplySnapshot(SalesCartSnapshot snapshot)
    {
        CartLines.Clear();

        foreach (var line in snapshot.Lines)
        {
            CartLines.Add(line);
        }

        Subtotal = FormatAmount(snapshot.SubtotalAmount, snapshot.Currency);
        DiscountTotal = FormatAmount(snapshot.DiscountTotalAmount, snapshot.Currency);
        TaxTotal = FormatAmount(snapshot.TaxTotalAmount, snapshot.Currency);
        GrandTotal = FormatAmount(snapshot.TotalAmount, snapshot.Currency);
        HasItems = snapshot.HasItems;
    }

    private void HandleUnexpectedError(Exception exception) => GeneralError = "Ocurrió un error inesperado.";

    private static string FormatAmount(decimal amount, string currency) =>
        $"{amount.ToString("N2", CultureInfo.CurrentCulture)} {currency}";

    private static string ToErrorMessage(SalesCartResultStatus status, decimal? availableQuantity) => status switch
    {
        SalesCartResultStatus.NotAuthenticated => "Debes iniciar sesión para continuar.",
        SalesCartResultStatus.RegisterSessionRequired => "Debes abrir la caja para continuar.",
        SalesCartResultStatus.ProductNotFound => "El producto no existe o no pertenece a esta organización.",
        SalesCartResultStatus.ProductInactive => "El producto no está activo.",
        SalesCartResultStatus.OutOfStock => "El producto no tiene existencia disponible.",
        SalesCartResultStatus.InsufficientStock => availableQuantity is { } quantity
            ? $"Existencia insuficiente. Disponible: {quantity.ToString(CultureInfo.CurrentCulture)}."
            : "No hay existencia suficiente para la cantidad solicitada.",
        SalesCartResultStatus.InvalidQuantity => "La cantidad debe ser mayor que cero.",
        SalesCartResultStatus.LineNotFound => "La línea ya no existe en el carrito.",
        SalesCartResultStatus.CurrencyMismatch => "El producto tiene una moneda distinta a la de la caja.",
        _ => "No fue posible completar la operación.",
    };
}
