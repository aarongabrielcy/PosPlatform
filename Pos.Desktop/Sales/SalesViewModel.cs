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

namespace Pos.Desktop.Sales;

// Extraído de MainWindowViewModel en TAREA 24C: toda la presentación de venta (búsqueda, carrito,
// atajos de Nuevo/Editar producto). El estado global del shell (sesión, caja, logout/cerrar caja)
// permanece en MainWindowViewModel. TAREA 25A agrega el botón Cobrar (CheckoutCommand), que solo
// solicita abrir CheckoutWindow: el checkout en sí vive en ICheckoutService/CheckoutViewModel.
public sealed class SalesViewModel : ViewModelBase
{
    private readonly ICurrentUserSession _session;
    private readonly ICurrentRegisterSession _currentRegisterSession;
    private readonly ISalesCartService _salesCartService;
    private readonly IProductManagementService _productManagementService;
    private readonly ICurrentSalesCart _currentSalesCart;
    private readonly AsyncRelayCommand _searchCommand;
    private readonly AsyncRelayCommand _addSelectedProductCommand;
    private readonly AsyncRelayCommand<SalesCartLine> _increaseQuantityCommand;
    private readonly AsyncRelayCommand<SalesCartLine> _decreaseQuantityCommand;
    private readonly AsyncRelayCommand<SalesCartLine> _removeLineCommand;
    private readonly AsyncRelayCommand _cancelSaleCommand;
    private readonly AsyncRelayCommand _newProductCommand;
    private readonly AsyncRelayCommand _editProductCommand;
    private readonly AsyncRelayCommand _checkoutCommand;
    private readonly TimeSpan _searchDebounceDelay;

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
    private CancellationTokenSource? _searchCts;

    public SalesViewModel(
        ICurrentUserSession session,
        ICurrentRegisterSession currentRegisterSession,
        ISalesCartService salesCartService,
        IProductManagementService productManagementService,
        ICurrentSalesCart currentSalesCart,
        TimeSpan? searchDebounceDelay = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _currentRegisterSession = currentRegisterSession ?? throw new ArgumentNullException(nameof(currentRegisterSession));
        _salesCartService = salesCartService ?? throw new ArgumentNullException(nameof(salesCartService));
        _productManagementService = productManagementService ?? throw new ArgumentNullException(nameof(productManagementService));
        _currentSalesCart = currentSalesCart ?? throw new ArgumentNullException(nameof(currentSalesCart));
        _searchDebounceDelay = searchDebounceDelay ?? TimeSpan.FromMilliseconds(250);

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
        _checkoutCommand = new AsyncRelayCommand(ExecuteCheckoutAsync, () => CanCheckout);

        CartLines = new ObservableCollection<SalesCartLine>();
        SearchResults = new ObservableCollection<ProductSearchResult>();
        ApplySnapshot(_currentSalesCart.Snapshot);
    }

    // El ViewModel nunca muestra MessageBox directamente; solo pide confirmación. El código
    // detrás de la vista decide cómo confirmarla y llama a ConfirmCancelSale().
    public event EventHandler? CancelSaleConfirmationRequested;

    // El ViewModel nunca abre ventanas: solo pide abrir CreateProductWindow. El shell reenvía el
    // evento hasta App.xaml.cs, único lugar que resuelve ventanas desde el contenedor de DI.
    public event EventHandler? NewProductRequested;

    // Igual patrón que NewProductRequested, pero para EditProductWindow.
    public event EventHandler<ProductId>? EditProductRequested;

    // Igual patrón que NewProductRequested/EditProductRequested: el ViewModel nunca abre
    // CheckoutWindow directamente, solo pide abrirla. El checkout en sí (crear Sale, descontar
    // inventario, etc.) vive por completo en ICheckoutService, nunca aquí.
    public event EventHandler? CheckoutRequested;

    // El ViewModel nunca conoce TextBox/Keyboard: solo pide que el foco regrese al buscador tras
    // agregar un producto exitosamente (TAREA 24F, sección 13). SalesView es quien decide cómo
    // enfocar (SearchBox.Focus()/Keyboard.Focus()).
    public event EventHandler? SearchFocusRequested;

    public ICommand SearchCommand => _searchCommand;

    public ICommand AddSelectedProductCommand => _addSelectedProductCommand;

    public ICommand IncreaseQuantityCommand => _increaseQuantityCommand;

    public ICommand DecreaseQuantityCommand => _decreaseQuantityCommand;

    public ICommand RemoveLineCommand => _removeLineCommand;

    public ICommand CancelSaleCommand => _cancelSaleCommand;

    public ICommand NewProductCommand => _newProductCommand;

    public ICommand EditProductCommand => _editProductCommand;

    public ICommand CheckoutCommand => _checkoutCommand;

    // Gobierna la visibilidad/habilitación del botón "Editar producto" y del checkbox "Incluir
    // inactivos": ambos son funcionalidad administrativa, no disponible para cualquier cajero.
    public bool CanManageProducts => _session.CurrentUser?.HasPermission(Permission.ManageProducts) ?? false;

    // Gobierna la habilitación del botón "Cobrar" (TAREA 25A sección 16): requiere carrito con
    // líneas, caja abierta y permiso ProcessSale. La revalidación real y autoritativa ocurre de
    // todas formas dentro de ICheckoutService: esto solo evita habilitar el botón en un estado
    // obviamente inválido.
    public bool CanCheckout =>
        HasItems
        && _currentRegisterSession.IsOpen
        && (_session.CurrentUser?.HasPermission(Permission.ProcessSale) ?? false);

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
                ScheduleDebouncedSearch();
            }
        }
    }

    // Expone la búsqueda en curso (inmediata o con debounce) para que las pruebas puedan esperar
    // de forma determinista sin depender de Thread.Sleep/delays reales (TAREA 24F, sección 27).
    // No tiene otro consumidor: la UI no la observa.
    internal Task PendingSearchTask { get; private set; } = Task.CompletedTask;

    public string? SearchStatusMessage
    {
        get => _searchStatusMessage;
        private set => SetProperty(ref _searchStatusMessage, value);
    }

    // Solo tiene efecto para usuarios con ManageProducts: SalesView la oculta/deshabilita para el
    // resto. La búsqueda del carrito (SalesCartService) nunca incluye inactivos.
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
        private set
        {
            if (SetProperty(ref _hasItems, value))
            {
                OnPropertyChanged(nameof(CanCheckout));
                _checkoutCommand.RaiseCanExecuteChanged();
            }
        }
    }

    // Llamado desde el código detrás de SalesView tras confirmar el diálogo. Cancelar solo limpia
    // el carrito: no cierra caja ni cierra sesión.
    public void ConfirmCancelSale()
    {
        var result = _salesCartService.Clear();
        ApplyResult(result);
    }

    // Llamado tras cerrar CreateProductWindow con éxito: coloca el SKU recién creado en el
    // buscador y ejecuta la búsqueda para que el producto aparezca de inmediato. No se agrega
    // automáticamente al carrito.
    public void ApplyProductCreated(string sku)
    {
        SearchText = sku;

        if (_searchCommand.CanExecute(null))
        {
            _searchCommand.Execute(null);
        }
    }

    // Llamado tras cerrar EditProductWindow (edición, activar/desactivar o ajuste de inventario):
    // refresca el buscador con el SKU del producto editado, igual que ApplyProductCreated. Si el
    // producto quedó inactivo y no se incluyen inactivos, desaparece del grid como se espera.
    public void ApplyProductUpdated(string sku) => ApplyProductCreated(sku);

    // Llamado tras cerrar CheckoutWindow con un cobro exitoso: CheckoutService ya limpió
    // CurrentSalesCart dentro de su único commit, así que aquí solo se refleja ese estado (carrito
    // vacío, totales en cero) en la UI, igual que ConfirmCancelSale.
    public void ApplyCheckoutCompleted() => ApplySnapshot(_currentSalesCart.Snapshot);

    // Búsqueda inmediata (Enter/botón Buscar, TAREA 24F sección 7): cancela cualquier debounce
    // pendiente y ejecuta la consulta sin esperar. AsyncRelayCommand espera este Task, así que al
    // volver de Execute() los resultados ya están aplicados (igual que antes de introducir
    // search-as-you-type).
    private async Task ExecuteSearchAsync()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();

        var trimmedSearchText = SearchText.Trim();

        if (trimmedSearchText.Length == 0)
        {
            _searchCts = null;
            SearchResults.Clear();
            SelectedSearchResult = null;
            SearchStatusMessage = "Escribe un SKU, código de barras o nombre para buscar.";
            GeneralError = null;
            PendingSearchTask = Task.CompletedTask;

            return;
        }

        var cts = new CancellationTokenSource();
        _searchCts = cts;
        var task = RunSearchCoreAsync(trimmedSearchText, cts.Token);
        PendingSearchTask = task;

        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
            // Una búsqueda más reciente reemplazó a esta: no es un error que deba mostrarse.
        }
    }

    // Búsqueda con debounce (TAREA 24F sección 3-4): se dispara desde el setter de SearchText.
    // Cancela la búsqueda/debounce anterior (CancellationTokenSource) para que resultados viejos
    // nunca sobrescriban a los nuevos, sin importar el orden en que terminen las consultas.
    private void ScheduleDebouncedSearch()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();

        var trimmedSearchText = SearchText.Trim();

        if (trimmedSearchText.Length == 0)
        {
            _searchCts = null;
            SearchResults.Clear();
            SelectedSearchResult = null;
            GeneralError = null;
            PendingSearchTask = Task.CompletedTask;

            return;
        }

        var cts = new CancellationTokenSource();
        _searchCts = cts;
        PendingSearchTask = RunDebouncedSearchAsync(trimmedSearchText, cts.Token);
    }

    private async Task RunDebouncedSearchAsync(string trimmedSearchText, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_searchDebounceDelay, cancellationToken);
            await RunSearchCoreAsync(trimmedSearchText, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Un nuevo carácter escrito canceló este debounce: comportamiento esperado, no un error.
        }
    }

    // Lógica de consulta compartida por la búsqueda inmediata y la de debounce. Vuelve a validar
    // la cancelación después de esperar la consulta (ThrowIfCancellationRequested) porque los
    // fakes/servicios no siempre observan el CancellationToken: así una respuesta tardía de un
    // término viejo nunca reemplaza los resultados de un término más nuevo (TAREA 24F sección 4).
    private async Task RunSearchCoreAsync(string trimmedSearchText, CancellationToken cancellationToken)
    {
        IsSearching = true;

        try
        {
            // "Incluir inactivos" solo tiene efecto para usuarios con ManageProducts; el resto
            // (y el propio carrito de venta) siempre buscan exclusivamente productos activos a
            // través de SalesCartService.
            var results = IncludeInactive && CanManageProducts
                ? await _productManagementService.SearchAsync(trimmedSearchText, includeInactive: true, cancellationToken)
                : await _salesCartService.SearchProductsAsync(trimmedSearchText, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

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

            // Selección automática cuando la búsqueda resulta en un único producto (TAREA 24F
            // sección 8): cubre tanto el lector de código de barras (SKU/barcode exacto + Enter)
            // como escribir hasta dejar un solo resultado. Nunca agrega al carrito por sí sola.
            SelectedSearchResult = SearchResults.Count == 1 ? SearchResults[0] : null;

            GeneralError = null;
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                IsSearching = false;
            }
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

            // Solo se limpia la búsqueda cuando Add fue exitoso (TAREA 24F sección 12): si falla
            // (stock insuficiente, producto inválido, etc.) el cajero necesita seguir viendo qué
            // producto intentó agregar.
            if (result.Success)
            {
                ClearSearchAfterAdd();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    // TAREA 24F sección 11/13: tras agregar exitosamente, limpiar buscador/resultados/selección y
    // pedir que el foco regrese al SearchBox. Reutiliza el mismo camino "vacío" de
    // ScheduleDebouncedSearch (a través del setter de SearchText) para no duplicar esa limpieza.
    private void ClearSearchAfterAdd()
    {
        SearchText = string.Empty;
        SearchFocusRequested?.Invoke(this, EventArgs.Empty);
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

    private Task ExecuteCheckoutAsync()
    {
        CheckoutRequested?.Invoke(this, EventArgs.Empty);

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
