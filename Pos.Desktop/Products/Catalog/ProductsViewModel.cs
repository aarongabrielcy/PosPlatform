using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Pos.Application.Authentication;
using Pos.Application.Products.ManageProduct;
using Pos.Desktop.Common;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;

namespace Pos.Desktop.Products.Catalog;

// Catálogo administrativo de productos (TAREA 24C): forma principal de administrar productos,
// no depende de que el usuario conozca previamente el SKU. Carga automáticamente al construirse
// y puede volver a refrescarse al navegar (ver MainWindowViewModel).
public sealed class ProductsViewModel : ViewModelBase
{
    public const int PageSize = 50;

    private readonly IProductManagementService _productManagementService;
    private readonly ICurrentUserSession _currentUserSession;
    private readonly AsyncRelayCommand _loadCommand;
    private readonly AsyncRelayCommand _searchCommand;
    private readonly AsyncRelayCommand _nextPageCommand;
    private readonly AsyncRelayCommand _previousPageCommand;
    private readonly AsyncRelayCommand _newProductCommand;
    private readonly AsyncRelayCommand<ProductCatalogItem> _editProductCommand;
    private readonly AsyncRelayCommand<ProductCatalogItem> _viewAuditDetailCommand;
    private readonly TimeSpan _searchDebounceDelay;

    private string _searchText = string.Empty;
    private ProductCatalogStatusFilter _selectedFilter = ProductCatalogStatusFilter.All;
    private ProductCatalogItem? _selectedProduct;
    private bool _isBusy;
    private string? _generalError;
    private string? _statusMessage;
    private int _currentPage = 1;
    private bool _canGoNext;
    private CancellationTokenSource? _searchCts;

    public ProductsViewModel(
        IProductManagementService productManagementService,
        ICurrentUserSession currentUserSession,
        TimeSpan? searchDebounceDelay = null)
    {
        _productManagementService = productManagementService ?? throw new ArgumentNullException(nameof(productManagementService));
        _currentUserSession = currentUserSession ?? throw new ArgumentNullException(nameof(currentUserSession));
        _searchDebounceDelay = searchDebounceDelay ?? TimeSpan.FromMilliseconds(250);

        // LoadCommand y SearchCommand ejecutan la misma carga inmediata (página 1 con el
        // filtro/término actuales), cancelando cualquier debounce pendiente: LoadCommand es el
        // punto seguro (con manejo de errores propio de AsyncRelayCommand) que usa el shell al
        // navegar a Productos, sin depender de que el usuario presione "Buscar".
        _loadCommand = new AsyncRelayCommand(() => ExecuteImmediateSearchAsync(resetToFirstPage: true), onError: HandleUnexpectedError);
        _searchCommand = new AsyncRelayCommand(() => ExecuteImmediateSearchAsync(resetToFirstPage: true), onError: HandleUnexpectedError);
        _nextPageCommand = new AsyncRelayCommand(ExecuteNextPageAsync, () => CanGoNext && !IsBusy, HandleUnexpectedError);
        _previousPageCommand = new AsyncRelayCommand(ExecutePreviousPageAsync, () => CanGoPrevious && !IsBusy, HandleUnexpectedError);
        _newProductCommand = new AsyncRelayCommand(ExecuteNewProductAsync, () => CanManageProducts);
        _editProductCommand = new AsyncRelayCommand<ProductCatalogItem>(
            ExecuteEditProductAsync, item => item is not null && CanManageProducts && !IsBusy, HandleUnexpectedError);
        _viewAuditDetailCommand = new AsyncRelayCommand<ProductCatalogItem>(
            ExecuteViewAuditDetailAsync, item => item is not null && !IsBusy, HandleUnexpectedError);

        Products = new ObservableCollection<ProductCatalogItem>();
    }

    // El ViewModel nunca abre ventanas: solo pide abrir CreateProductWindow/EditProductWindow. El
    // shell reenvía el evento hasta App.xaml.cs, igual que en SalesViewModel.
    public event EventHandler? NewProductRequested;

    public event EventHandler<ProductId>? EditProductRequested;

    // "Ver detalle" desde el indicador de actividad reciente (TAREA 24D, sección 32/33): el shell
    // navega a Auditoría > Productos preaplicando el filtro por ProductId, sin abrir ninguna
    // ventana nueva (no hay ProductHistoryWindow).
    public event EventHandler<ProductCatalogItem>? AuditRequested;

    public ICommand LoadCommand => _loadCommand;

    public ICommand SearchCommand => _searchCommand;

    public ICommand NextPageCommand => _nextPageCommand;

    public ICommand PreviousPageCommand => _previousPageCommand;

    public ICommand NewProductCommand => _newProductCommand;

    public ICommand EditProductCommand => _editProductCommand;

    public ICommand ViewAuditDetailCommand => _viewAuditDetailCommand;

    public ObservableCollection<ProductCatalogItem> Products { get; }

    // Botones "+ Nuevo producto" / "Editar producto" (READ-ONLY CORRECTION, sección 8/16 de la
    // tarea): visibles/habilitados solo con ManageProducts, igual patrón que
    // InventoryViewModel.CanAdjustInventory. Un usuario con solo ViewProducts (p. ej. Cashier)
    // puede consultar el catálogo pero no mutar productos; ProductManagementService ya rechaza la
    // mutación del lado servidor si de todos modos se invocara (defensa en profundidad).
    public bool CanManageProducts => _currentUserSession.CurrentUser?.HasPermission(Permission.ManageProducts) ?? false;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ScheduleDebouncedSearch();
            }
        }
    }

    // Expone la búsqueda en curso (inmediata o con debounce) para que las pruebas puedan esperar
    // de forma determinista sin depender de Thread.Sleep/delays reales (TAREA 24F, sección 27).
    // No tiene otro consumidor: la UI no la observa.
    internal Task PendingSearchTask { get; private set; } = Task.CompletedTask;

    public ProductCatalogStatusFilter SelectedFilter
    {
        get => _selectedFilter;
        set
        {
            if (SetProperty(ref _selectedFilter, value) && _searchCommand.CanExecute(null))
            {
                _searchCommand.Execute(null);
            }
        }
    }

    public ProductCatalogItem? SelectedProduct
    {
        get => _selectedProduct;
        set
        {
            if (SetProperty(ref _selectedProduct, value))
            {
                _editProductCommand.RaiseCanExecuteChanged();
            }
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
                _nextPageCommand.RaiseCanExecuteChanged();
                _previousPageCommand.RaiseCanExecuteChanged();
                _editProductCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsNotBusy => !IsBusy;

    public string? GeneralError
    {
        get => _generalError;
        private set => SetProperty(ref _generalError, value);
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public int CurrentPage
    {
        get => _currentPage;
        private set
        {
            if (SetProperty(ref _currentPage, value))
            {
                OnPropertyChanged(nameof(CanGoPrevious));
                _previousPageCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool CanGoPrevious => CurrentPage > 1;

    public bool CanGoNext
    {
        get => _canGoNext;
        private set
        {
            if (SetProperty(ref _canGoNext, value))
            {
                _nextPageCommand.RaiseCanExecuteChanged();
            }
        }
    }

    // Llamado al construir el shell y cada vez que se navega a Productos (ver
    // MainWindowViewModel): no exige una búsqueda manual previa (TAREA 24C, sección 9).
    public Task RefreshAsync() => ExecuteImmediateSearchAsync(resetToFirstPage: true);

    // Llamado tras cerrar CreateProductWindow con éxito: refresca el catálogo con el SKU recién
    // creado como término de búsqueda para localizarlo y, si la búsqueda resulta en un único
    // producto, seleccionarlo.
    public void ApplyProductCreated(string sku)
    {
        SearchText = sku;

        if (_searchCommand.CanExecute(null))
        {
            _searchCommand.Execute(null);
        }
    }

    // Llamado tras cerrar EditProductWindow (edición, activar/desactivar o ajuste de inventario):
    // mismo refresco que ApplyProductCreated.
    public void ApplyProductUpdated(string sku) => ApplyProductCreated(sku);

    // Búsqueda con debounce (TAREA 24F sección 16-17): se dispara desde el setter de SearchText.
    // A diferencia de Venta, un término vacío en Productos sí debe recargar el catálogo completo
    // (ProductsView soporta listado sin búsqueda), así que aquí no hay atajo de "vacío = limpiar".
    private void ScheduleDebouncedSearch()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();

        var cts = new CancellationTokenSource();
        _searchCts = cts;
        PendingSearchTask = RunDebouncedSearchAsync(cts.Token);
    }

    private async Task RunDebouncedSearchAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_searchDebounceDelay, cancellationToken);
            await LoadPageAsync(resetToFirstPage: true, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Un nuevo carácter escrito canceló este debounce: comportamiento esperado, no un error.
        }
    }

    // Búsqueda/paginación inmediata (LoadCommand, SearchCommand, filtro, paginar, refrescar tras
    // crear/editar): cancela cualquier debounce pendiente para que un término viejo nunca
    // sobrescriba el resultado de una acción explícita más reciente (TAREA 24F sección 4/7).
    private async Task ExecuteImmediateSearchAsync(bool resetToFirstPage)
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();

        var cts = new CancellationTokenSource();
        _searchCts = cts;
        var task = LoadPageAsync(resetToFirstPage, cts.Token);
        PendingSearchTask = task;

        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
            // Una carga más reciente reemplazó a esta: no es un error que deba mostrarse.
        }
    }

    private async Task LoadPageAsync(bool resetToFirstPage, CancellationToken cancellationToken)
    {
        if (resetToFirstPage)
        {
            CurrentPage = 1;
        }

        IsBusy = true;

        try
        {
            var skip = (CurrentPage - 1) * PageSize;
            var result = await _productManagementService.GetCatalogPageAsync(
                SearchText, SelectedFilter, skip, PageSize, cancellationToken);

            // Se revalida después de esperar la consulta porque los fakes/servicios no siempre
            // observan el CancellationToken: así una respuesta tardía de un término viejo nunca
            // reemplaza los resultados de una búsqueda más nueva (TAREA 24F sección 4).
            cancellationToken.ThrowIfCancellationRequested();

            var previouslySelectedId = SelectedProduct?.ProductId;

            Products.Clear();

            foreach (var item in result.Items)
            {
                Products.Add(item);
            }

            CanGoNext = result.HasNextPage;

            StatusMessage = Products.Count switch
            {
                0 => "No se encontraron productos.",
                1 => "1 producto mostrado.",
                _ => $"{Products.Count} productos mostrados.",
            };

            // Conserva la selección anterior si el producto sigue en la página actual (TAREA 24F
            // sección 19, p.ej. tras editar sin que cambie el filtro/búsqueda). Si ya no aparece,
            // se limpia sin caer en el criterio de "único resultado": ese criterio es solo para
            // cuando no había una selección previa que ya no corresponde (p.ej. nueva búsqueda).
            SelectedProduct = previouslySelectedId is { } id
                ? Products.FirstOrDefault(p => p.ProductId == id)
                : Products.Count == 1 ? Products[0] : null;

            GeneralError = null;
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                IsBusy = false;
            }
        }
    }

    private Task ExecuteNextPageAsync()
    {
        CurrentPage++;
        return ExecuteImmediateSearchAsync(resetToFirstPage: false);
    }

    private Task ExecutePreviousPageAsync()
    {
        if (CurrentPage <= 1)
        {
            return Task.CompletedTask;
        }

        CurrentPage--;
        return ExecuteImmediateSearchAsync(resetToFirstPage: false);
    }

    private Task ExecuteNewProductAsync()
    {
        NewProductRequested?.Invoke(this, EventArgs.Empty);

        return Task.CompletedTask;
    }

    private Task ExecuteEditProductAsync(ProductCatalogItem? item)
    {
        if (item is not null)
        {
            EditProductRequested?.Invoke(this, item.ProductId);
        }

        return Task.CompletedTask;
    }

    private Task ExecuteViewAuditDetailAsync(ProductCatalogItem? item)
    {
        if (item is not null)
        {
            AuditRequested?.Invoke(this, item);
        }

        return Task.CompletedTask;
    }

    private void HandleUnexpectedError(Exception exception) => GeneralError = "Ocurrió un error inesperado.";
}
