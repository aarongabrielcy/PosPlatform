using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Pos.Application.Authentication;
using Pos.Application.Inventory;
using Pos.Desktop.Common;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Inventory;
using Pos.Domain.Security;

namespace Pos.Desktop.Inventory;

// Módulo operativo de Inventario (TAREA 24G): Existencias + Movimientos. Nunca modifica
// InventoryItem/InventoryMovement por sí mismo: el ajuste se delega a AdjustInventoryWindow (mismo
// que usa Productos), abierto por el shell tras AdjustInventoryRequested, igual patrón que
// NewProductRequested/EditProductRequested en SalesViewModel/ProductsViewModel (sección 16).
public sealed class InventoryViewModel : ViewModelBase
{
    public const int PageSize = 50;

    private readonly IInventoryService _inventoryService;
    private readonly ICurrentUserSession _currentUserSession;
    private readonly TimeSpan _searchDebounceDelay;

    private readonly AsyncRelayCommand _loadCommand;
    private readonly AsyncRelayCommand _searchCommand;
    private readonly AsyncRelayCommand _nextPageCommand;
    private readonly AsyncRelayCommand _previousPageCommand;
    private readonly AsyncRelayCommand _clearFiltersCommand;
    private readonly AsyncRelayCommand<InventoryCatalogRowViewModel> _adjustCommand;
    private readonly AsyncRelayCommand<InventoryCatalogRowViewModel> _viewMovementsCommand;

    private readonly AsyncRelayCommand _movementSearchCommand;
    private readonly AsyncRelayCommand _movementNextPageCommand;
    private readonly AsyncRelayCommand _movementPreviousPageCommand;
    private readonly AsyncRelayCommand _clearMovementFiltersCommand;
    private readonly AsyncRelayCommand _clearMovementProductFilterCommand;
    private readonly AsyncRelayCommand _applyInventoryAdjustedCommand;

    private CancellationTokenSource? _searchCts;

    private string _searchText = string.Empty;
    private InventoryCatalogStatusFilter _selectedStockFilter = InventoryCatalogStatusFilter.All;
    private InventoryCatalogRowViewModel? _selectedItem;
    private bool _isBusy;
    private string? _generalError;
    private string? _statusMessage;
    private int _currentPage = 1;
    private bool _canGoNext;
    private int _selectedTabIndex;

    private int _trackedProductsCount;
    private int _inStockCount;
    private int _lowStockCount;
    private int _outOfStockCount;

    private string _movementSearchText = string.Empty;
    private InventoryMovementType? _selectedMovementType;
    private DateTime? _movementFromDate;
    private DateTime? _movementToDate;
    private ProductId? _movementProductIdFilter;
    private string? _movementProductFilterLabel;
    private int _movementCurrentPage = 1;
    private bool _movementCanGoNext;

    public InventoryViewModel(
        IInventoryService inventoryService,
        ICurrentUserSession currentUserSession,
        TimeSpan? searchDebounceDelay = null)
    {
        _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
        _currentUserSession = currentUserSession ?? throw new ArgumentNullException(nameof(currentUserSession));
        _searchDebounceDelay = searchDebounceDelay ?? TimeSpan.FromMilliseconds(250);

        _loadCommand = new AsyncRelayCommand(ExecuteLoadAsync, onError: HandleUnexpectedError);
        _searchCommand = new AsyncRelayCommand(() => ExecuteImmediateSearchAsync(resetToFirstPage: true), onError: HandleUnexpectedError);
        _nextPageCommand = new AsyncRelayCommand(ExecuteNextPageAsync, () => CanGoNext && !IsBusy, HandleUnexpectedError);
        _previousPageCommand = new AsyncRelayCommand(ExecutePreviousPageAsync, () => CanGoPrevious && !IsBusy, HandleUnexpectedError);
        _clearFiltersCommand = new AsyncRelayCommand(ExecuteClearFiltersAsync, onError: HandleUnexpectedError);
        _adjustCommand = new AsyncRelayCommand<InventoryCatalogRowViewModel>(
            ExecuteAdjustAsync, item => item is not null && CanAdjustInventory && !IsBusy);
        _viewMovementsCommand = new AsyncRelayCommand<InventoryCatalogRowViewModel>(
            ExecuteViewMovementsAsync, item => item is not null && !IsBusy);

        _movementSearchCommand = new AsyncRelayCommand(() => ExecuteMovementSearchAsync(resetToFirstPage: true), onError: HandleUnexpectedError);
        _movementNextPageCommand = new AsyncRelayCommand(ExecuteMovementNextPageAsync, () => MovementCanGoNext && !IsBusy, HandleUnexpectedError);
        _movementPreviousPageCommand = new AsyncRelayCommand(ExecuteMovementPreviousPageAsync, () => MovementCanGoPrevious && !IsBusy, HandleUnexpectedError);
        _clearMovementFiltersCommand = new AsyncRelayCommand(ExecuteClearMovementFiltersAsync, onError: HandleUnexpectedError);
        _clearMovementProductFilterCommand = new AsyncRelayCommand(ExecuteClearMovementProductFilterAsync, onError: HandleUnexpectedError);
        _applyInventoryAdjustedCommand = new AsyncRelayCommand(ExecuteApplyInventoryAdjustedAsync, onError: HandleUnexpectedError);

        Items = new ObservableCollection<InventoryCatalogRowViewModel>();
        Movements = new ObservableCollection<InventoryMovementRowViewModel>();
    }

    // El ViewModel nunca abre ventanas: solo pide abrir AdjustInventoryWindow. El shell reenvía el
    // evento hasta App.xaml.cs, igual patrón que ProductsViewModel.EditProductRequested.
    public event EventHandler<InventoryCatalogItem>? AdjustInventoryRequested;

    public ICommand LoadCommand => _loadCommand;

    public ICommand SearchCommand => _searchCommand;

    public ICommand NextPageCommand => _nextPageCommand;

    public ICommand PreviousPageCommand => _previousPageCommand;

    public ICommand ClearFiltersCommand => _clearFiltersCommand;

    public ICommand AdjustCommand => _adjustCommand;

    public ICommand ViewMovementsCommand => _viewMovementsCommand;

    public ICommand MovementSearchCommand => _movementSearchCommand;

    public ICommand MovementNextPageCommand => _movementNextPageCommand;

    public ICommand MovementPreviousPageCommand => _movementPreviousPageCommand;

    public ICommand ClearMovementFiltersCommand => _clearMovementFiltersCommand;

    public ICommand ClearMovementProductFilterCommand => _clearMovementProductFilterCommand;

    public ObservableCollection<InventoryCatalogRowViewModel> Items { get; }

    public ObservableCollection<InventoryMovementRowViewModel> Movements { get; }

    // Botón "Ajustar existencia" (TAREA 24G, sección 18): visible/habilitado solo con
    // AdjustInventory. Un usuario con ManageProducts (sin AdjustInventory) puede consultar pero no
    // ajustar.
    public bool CanAdjustInventory => _currentUserSession.CurrentUser?.HasPermission(Permission.AdjustInventory) ?? false;

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

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

    // Expone la búsqueda en curso (inmediata o con debounce) para pruebas deterministas, igual
    // patrón que ProductsViewModel.PendingSearchTask (TAREA 24F, sección 27).
    internal Task PendingSearchTask { get; private set; } = Task.CompletedTask;

    public InventoryCatalogStatusFilter SelectedStockFilter
    {
        get => _selectedStockFilter;
        set
        {
            if (SetProperty(ref _selectedStockFilter, value) && _searchCommand.CanExecute(null))
            {
                _searchCommand.Execute(null);
            }
        }
    }

    public InventoryCatalogRowViewModel? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value))
            {
                _adjustCommand.RaiseCanExecuteChanged();
                _viewMovementsCommand.RaiseCanExecuteChanged();
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
                _adjustCommand.RaiseCanExecuteChanged();
                _viewMovementsCommand.RaiseCanExecuteChanged();
                _movementNextPageCommand.RaiseCanExecuteChanged();
                _movementPreviousPageCommand.RaiseCanExecuteChanged();
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

    // KPIs del scope actual (Organization+Branch), no de la página visible (TAREA 24G, sección 12).
    public int TrackedProductsCount
    {
        get => _trackedProductsCount;
        private set => SetProperty(ref _trackedProductsCount, value);
    }

    public int InStockCount
    {
        get => _inStockCount;
        private set => SetProperty(ref _inStockCount, value);
    }

    public int LowStockCount
    {
        get => _lowStockCount;
        private set => SetProperty(ref _lowStockCount, value);
    }

    public int OutOfStockCount
    {
        get => _outOfStockCount;
        private set => SetProperty(ref _outOfStockCount, value);
    }

    public string MovementSearchText
    {
        get => _movementSearchText;
        set => SetProperty(ref _movementSearchText, value);
    }

    public InventoryMovementType? SelectedMovementType
    {
        get => _selectedMovementType;
        set => SetProperty(ref _selectedMovementType, value);
    }

    public DateTime? MovementFromDate
    {
        get => _movementFromDate;
        set => SetProperty(ref _movementFromDate, value);
    }

    public DateTime? MovementToDate
    {
        get => _movementToDate;
        set => SetProperty(ref _movementToDate, value);
    }

    public bool HasMovementProductFilter => _movementProductIdFilter is not null;

    public string? MovementProductFilterLabel
    {
        get => _movementProductFilterLabel;
        private set => SetProperty(ref _movementProductFilterLabel, value);
    }

    public int MovementCurrentPage
    {
        get => _movementCurrentPage;
        private set
        {
            if (SetProperty(ref _movementCurrentPage, value))
            {
                OnPropertyChanged(nameof(MovementCanGoPrevious));
                _movementPreviousPageCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool MovementCanGoPrevious => MovementCurrentPage > 1;

    public bool MovementCanGoNext
    {
        get => _movementCanGoNext;
        private set
        {
            if (SetProperty(ref _movementCanGoNext, value))
            {
                _movementNextPageCommand.RaiseCanExecuteChanged();
            }
        }
    }

    // Llamado al navegar a Inventario (ver MainWindowViewModel, igual patrón que
    // ProductsViewModel/ProductAuditViewModel): carga Existencias, KPIs y Movimientos globales.
    private async Task ExecuteLoadAsync()
    {
        OnPropertyChanged(nameof(CanAdjustInventory));
        await ExecuteImmediateSearchAsync(resetToFirstPage: true);
        await LoadMovementPageAsync(resetToFirstPage: true);
    }

    // Llamado desde MainWindowViewModel tras confirmar un ajuste en AdjustInventoryWindow (TAREA
    // 24G, sección 19/41): refresca fila+KPIs conservando búsqueda/filtro/página, y refresca
    // Movimientos si esa pestaña está visible. No inserta Notification/Audit por su cuenta: eso ya
    // ocurrió dentro de ProductManagementService.AdjustInventoryAsync (una sola operación, un solo
    // commit). Punto de entrada síncrono (dispara el comando y no espera), igual patrón que
    // ProductsViewModel.ApplyProductUpdated -> _searchCommand.Execute(null).
    public void ApplyInventoryAdjusted()
    {
        if (_applyInventoryAdjustedCommand.CanExecute(null))
        {
            _applyInventoryAdjustedCommand.Execute(null);
        }
    }

    private async Task ExecuteApplyInventoryAdjustedAsync()
    {
        await ExecuteImmediateSearchAsync(resetToFirstPage: false);

        if (SelectedTabIndex == 1)
        {
            await LoadMovementPageAsync(resetToFirstPage: false);
        }
    }

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
            await LoadCatalogPageAsync(resetToFirstPage: true, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Un nuevo carácter escrito canceló este debounce: comportamiento esperado, no un error.
        }
    }

    private async Task ExecuteImmediateSearchAsync(bool resetToFirstPage)
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();

        var cts = new CancellationTokenSource();
        _searchCts = cts;
        var task = LoadCatalogPageAsync(resetToFirstPage, cts.Token);
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

    private async Task LoadCatalogPageAsync(bool resetToFirstPage, CancellationToken cancellationToken)
    {
        if (resetToFirstPage)
        {
            CurrentPage = 1;
        }

        IsBusy = true;

        try
        {
            var skip = (CurrentPage - 1) * PageSize;

            var pageTask = _inventoryService.GetCatalogPageAsync(
                SearchText, SelectedStockFilter, skip, PageSize, cancellationToken);
            var summaryTask = _inventoryService.GetSummaryAsync(cancellationToken);

            var page = await pageTask;
            var summary = await summaryTask;

            // Revalidado después de esperar porque los fakes/servicios no siempre observan el
            // CancellationToken (TAREA 24F, sección 4): una respuesta tardía de un término viejo
            // nunca reemplaza los resultados de una búsqueda más nueva.
            cancellationToken.ThrowIfCancellationRequested();

            var previouslySelectedId = SelectedItem?.Item.ProductId;

            Items.Clear();

            foreach (var item in page.Items)
            {
                Items.Add(new InventoryCatalogRowViewModel(item));
            }

            CanGoNext = page.HasNextPage;

            TrackedProductsCount = summary.TrackedProductsCount;
            InStockCount = summary.InStockCount;
            LowStockCount = summary.LowStockCount;
            OutOfStockCount = summary.OutOfStockCount;

            StatusMessage = Items.Count switch
            {
                0 => "No se encontraron productos con inventario.",
                1 => "1 producto mostrado.",
                _ => $"{Items.Count} productos mostrados.",
            };

            // Conserva la selección si el producto sigue en la página actual/filtro; si ya no
            // corresponde (p. ej. un ajuste lo sacó de "Stock bajo"), se limpia sin forzarlo dentro
            // de un filtro que ya no cumple (TAREA 24G, sección 35).
            SelectedItem = previouslySelectedId is { } id
                ? Items.FirstOrDefault(i => i.Item.ProductId == id)
                : Items.Count == 1 ? Items[0] : null;

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

    private Task ExecuteClearFiltersAsync()
    {
        _searchText = string.Empty;
        _selectedStockFilter = InventoryCatalogStatusFilter.All;

        OnPropertyChanged(nameof(SearchText));
        OnPropertyChanged(nameof(SelectedStockFilter));

        return ExecuteImmediateSearchAsync(resetToFirstPage: true);
    }

    private Task ExecuteAdjustAsync(InventoryCatalogRowViewModel? item)
    {
        if (item is not null && CanAdjustInventory)
        {
            AdjustInventoryRequested?.Invoke(this, item.Item);
        }

        return Task.CompletedTask;
    }

    // "Ver movimientos" desde una fila de Existencias (TAREA 24G, sección 24/25): preaplica el
    // filtro por ProductId y navega a la pestaña Movimientos, sin abrir ninguna ventana nueva.
    private async Task ExecuteViewMovementsAsync(InventoryCatalogRowViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        _movementProductIdFilter = item.Item.ProductId;
        MovementProductFilterLabel = $"Producto: {item.Item.Sku}";
        OnPropertyChanged(nameof(HasMovementProductFilter));

        SelectedTabIndex = 1;

        await LoadMovementPageAsync(resetToFirstPage: true);
    }

    private Task ExecuteMovementSearchAsync(bool resetToFirstPage) => LoadMovementPageAsync(resetToFirstPage);

    private Task ExecuteMovementNextPageAsync()
    {
        MovementCurrentPage++;
        return LoadMovementPageAsync(resetToFirstPage: false);
    }

    private Task ExecuteMovementPreviousPageAsync()
    {
        if (MovementCurrentPage <= 1)
        {
            return Task.CompletedTask;
        }

        MovementCurrentPage--;
        return LoadMovementPageAsync(resetToFirstPage: false);
    }

    private Task ExecuteClearMovementFiltersAsync()
    {
        _movementSearchText = string.Empty;
        _selectedMovementType = null;
        _movementFromDate = null;
        _movementToDate = null;
        _movementProductIdFilter = null;
        MovementProductFilterLabel = null;

        OnPropertyChanged(nameof(MovementSearchText));
        OnPropertyChanged(nameof(SelectedMovementType));
        OnPropertyChanged(nameof(MovementFromDate));
        OnPropertyChanged(nameof(MovementToDate));
        OnPropertyChanged(nameof(HasMovementProductFilter));

        return LoadMovementPageAsync(resetToFirstPage: true);
    }

    private Task ExecuteClearMovementProductFilterAsync()
    {
        _movementProductIdFilter = null;
        MovementProductFilterLabel = null;
        OnPropertyChanged(nameof(HasMovementProductFilter));

        return LoadMovementPageAsync(resetToFirstPage: true);
    }

    private async Task LoadMovementPageAsync(bool resetToFirstPage)
    {
        if (resetToFirstPage)
        {
            MovementCurrentPage = 1;
        }

        IsBusy = true;

        try
        {
            var filter = new InventoryMovementFilter(
                ProductId: _movementProductIdFilter,
                SearchTerm: string.IsNullOrWhiteSpace(MovementSearchText) ? null : MovementSearchText.Trim(),
                Type: SelectedMovementType,
                FromUtc: ToStartOfDayUtc(MovementFromDate),
                ToUtc: ToEndOfDayUtc(MovementToDate));

            var skip = (MovementCurrentPage - 1) * PageSize;
            var result = await _inventoryService.GetMovementPageAsync(filter, skip, PageSize);

            Movements.Clear();

            foreach (var item in result.Items)
            {
                Movements.Add(new InventoryMovementRowViewModel(item));
            }

            MovementCanGoNext = result.HasNextPage;

            GeneralError = null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static DateTimeOffset? ToStartOfDayUtc(DateTime? localDate)
    {
        if (localDate is not { } value)
        {
            return null;
        }

        return new DateTimeOffset(value.Date, TimeZoneInfo.Local.GetUtcOffset(value.Date)).ToUniversalTime();
    }

    private static DateTimeOffset? ToEndOfDayUtc(DateTime? localDate)
    {
        if (localDate is not { } value)
        {
            return null;
        }

        var endOfDay = value.Date.AddDays(1).AddTicks(-1);

        return new DateTimeOffset(endOfDay, TimeZoneInfo.Local.GetUtcOffset(endOfDay)).ToUniversalTime();
    }

    private void HandleUnexpectedError(Exception exception) => GeneralError = "Ocurrió un error inesperado.";
}
