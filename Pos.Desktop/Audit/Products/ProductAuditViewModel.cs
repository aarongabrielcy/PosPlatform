using System.Collections.ObjectModel;
using System.Windows.Input;
using Pos.Application.ProductAudit;
using Pos.Desktop.Common;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.ProductAudit;

namespace Pos.Desktop.Audit.Products;

// Auditoría > Productos (TAREA 24D): módulo administrativo global, filtrado opcionalmente por
// ProductId cuando se navega desde el indicador de actividad reciente de Productos (sección 32).
// No abre ninguna ventana de historial separada: es la única pantalla que muestra auditoría.
public sealed class ProductAuditViewModel : ViewModelBase
{
    public const int PageSize = 50;

    private readonly IProductAuditService _productAuditService;
    private readonly AsyncRelayCommand _loadCommand;
    private readonly AsyncRelayCommand _searchCommand;
    private readonly AsyncRelayCommand _nextPageCommand;
    private readonly AsyncRelayCommand _previousPageCommand;
    private readonly AsyncRelayCommand _clearFiltersCommand;
    private readonly AsyncRelayCommand _clearProductFilterCommand;

    private string _searchText = string.Empty;
    private string _actorSearchText = string.Empty;
    private ProductAuditAction? _selectedAction;
    private DateTime? _fromDate;
    private DateTime? _toDate;
    private ProductId? _productIdFilter;
    private string? _productFilterLabel;
    private ProductAuditRowViewModel? _selectedEntry;
    private bool _isBusy;
    private string? _generalError;
    private string? _statusMessage;
    private int _currentPage = 1;
    private bool _canGoNext;

    public ProductAuditViewModel(IProductAuditService productAuditService)
    {
        _productAuditService = productAuditService ?? throw new ArgumentNullException(nameof(productAuditService));

        _loadCommand = new AsyncRelayCommand(() => LoadPageAsync(resetToFirstPage: true), onError: HandleUnexpectedError);
        _searchCommand = new AsyncRelayCommand(() => LoadPageAsync(resetToFirstPage: true), onError: HandleUnexpectedError);
        _nextPageCommand = new AsyncRelayCommand(ExecuteNextPageAsync, () => CanGoNext && !IsBusy, HandleUnexpectedError);
        _previousPageCommand = new AsyncRelayCommand(ExecutePreviousPageAsync, () => CanGoPrevious && !IsBusy, HandleUnexpectedError);
        _clearFiltersCommand = new AsyncRelayCommand(ExecuteClearFiltersAsync, onError: HandleUnexpectedError);
        _clearProductFilterCommand = new AsyncRelayCommand(ExecuteClearProductFilterAsync, onError: HandleUnexpectedError);

        Entries = new ObservableCollection<ProductAuditRowViewModel>();
    }

    public ICommand LoadCommand => _loadCommand;

    public ICommand SearchCommand => _searchCommand;

    public ICommand NextPageCommand => _nextPageCommand;

    public ICommand PreviousPageCommand => _previousPageCommand;

    public ICommand ClearFiltersCommand => _clearFiltersCommand;

    public ICommand ClearProductFilterCommand => _clearProductFilterCommand;

    public ObservableCollection<ProductAuditRowViewModel> Entries { get; }

    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    public string ActorSearchText
    {
        get => _actorSearchText;
        set => SetProperty(ref _actorSearchText, value);
    }

    public ProductAuditAction? SelectedAction
    {
        get => _selectedAction;
        set => SetProperty(ref _selectedAction, value);
    }

    public DateTime? FromDate
    {
        get => _fromDate;
        set => SetProperty(ref _fromDate, value);
    }

    public DateTime? ToDate
    {
        get => _toDate;
        set => SetProperty(ref _toDate, value);
    }

    public bool HasProductFilter => _productIdFilter is not null;

    public string? ProductFilterLabel
    {
        get => _productFilterLabel;
        private set => SetProperty(ref _productFilterLabel, value);
    }

    public ProductAuditRowViewModel? SelectedEntry
    {
        get => _selectedEntry;
        set => SetProperty(ref _selectedEntry, value);
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

    // Llamado al navegar a Auditoría > Productos (carga inicial o al volver a la pantalla): no
    // toca el filtro de producto ya aplicado, si lo hay.
    public Task RefreshAsync() => LoadPageAsync(resetToFirstPage: true);

    // Llamado desde MainWindowViewModel cuando ProductsViewModel emite AuditRequested (TAREA 24D,
    // sección 32/33): preaplica el filtro por ProductId antes de que el shell navegue a esta
    // pantalla.
    public void ApplyProductFilter(ProductId productId, string productSku)
    {
        _productIdFilter = productId;
        ProductFilterLabel = $"Producto: {productSku}";
        OnPropertyChanged(nameof(HasProductFilter));
    }

    private Task ExecuteClearFiltersAsync()
    {
        _searchText = string.Empty;
        _actorSearchText = string.Empty;
        _selectedAction = null;
        _fromDate = null;
        _toDate = null;
        _productIdFilter = null;
        ProductFilterLabel = null;

        OnPropertyChanged(nameof(SearchText));
        OnPropertyChanged(nameof(ActorSearchText));
        OnPropertyChanged(nameof(SelectedAction));
        OnPropertyChanged(nameof(FromDate));
        OnPropertyChanged(nameof(ToDate));
        OnPropertyChanged(nameof(HasProductFilter));

        return LoadPageAsync(resetToFirstPage: true);
    }

    private Task ExecuteClearProductFilterAsync()
    {
        _productIdFilter = null;
        ProductFilterLabel = null;
        OnPropertyChanged(nameof(HasProductFilter));

        return LoadPageAsync(resetToFirstPage: true);
    }

    private async Task LoadPageAsync(bool resetToFirstPage)
    {
        if (resetToFirstPage)
        {
            CurrentPage = 1;
        }

        IsBusy = true;

        try
        {
            var filter = new ProductAuditFilter(
                ProductId: _productIdFilter,
                SearchTerm: string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
                ActorUserId: null,
                ActorSearchTerm: string.IsNullOrWhiteSpace(ActorSearchText) ? null : ActorSearchText.Trim(),
                Action: SelectedAction,
                FromUtc: ToStartOfDayUtc(FromDate),
                ToUtc: ToEndOfDayUtc(ToDate));

            var skip = (CurrentPage - 1) * PageSize;
            var result = await _productAuditService.SearchPageAsync(filter, skip, PageSize);

            Entries.Clear();

            foreach (var entry in result.Items)
            {
                Entries.Add(new ProductAuditRowViewModel(entry));
            }

            CanGoNext = result.HasNextPage;

            StatusMessage = Entries.Count switch
            {
                0 => "No se encontraron eventos de auditoría.",
                1 => "1 evento mostrado.",
                _ => $"{Entries.Count} eventos mostrados.",
            };

            SelectedEntry = Entries.Count == 1 ? Entries[0] : null;

            GeneralError = null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExecuteNextPageAsync()
    {
        CurrentPage++;
        await LoadPageAsync(resetToFirstPage: false);
    }

    private async Task ExecutePreviousPageAsync()
    {
        if (CurrentPage <= 1)
        {
            return;
        }

        CurrentPage--;
        await LoadPageAsync(resetToFirstPage: false);
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
