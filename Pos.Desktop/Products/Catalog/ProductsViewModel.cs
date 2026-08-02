using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Pos.Application.Products.ManageProduct;
using Pos.Desktop.Common;
using Pos.Domain.Common.Identifiers;

namespace Pos.Desktop.Products.Catalog;

// Catálogo administrativo de productos (TAREA 24C): forma principal de administrar productos,
// no depende de que el usuario conozca previamente el SKU. Carga automáticamente al construirse
// y puede volver a refrescarse al navegar (ver MainWindowViewModel).
public sealed class ProductsViewModel : ViewModelBase
{
    public const int PageSize = 50;

    private readonly IProductManagementService _productManagementService;
    private readonly AsyncRelayCommand _loadCommand;
    private readonly AsyncRelayCommand _searchCommand;
    private readonly AsyncRelayCommand _nextPageCommand;
    private readonly AsyncRelayCommand _previousPageCommand;
    private readonly AsyncRelayCommand _newProductCommand;
    private readonly AsyncRelayCommand<ProductCatalogItem> _editProductCommand;

    private string _searchText = string.Empty;
    private ProductCatalogStatusFilter _selectedFilter = ProductCatalogStatusFilter.All;
    private ProductCatalogItem? _selectedProduct;
    private bool _isBusy;
    private string? _generalError;
    private string? _statusMessage;
    private int _currentPage = 1;
    private bool _canGoNext;

    public ProductsViewModel(IProductManagementService productManagementService)
    {
        _productManagementService = productManagementService ?? throw new ArgumentNullException(nameof(productManagementService));

        // LoadCommand y SearchCommand ejecutan la misma carga (página 1 con el filtro/término
        // actuales): LoadCommand es el punto seguro (con manejo de errores propio de
        // AsyncRelayCommand) que usa el shell al navegar a Productos, sin depender de que el
        // usuario presione "Buscar".
        _loadCommand = new AsyncRelayCommand(() => LoadPageAsync(resetToFirstPage: true), onError: HandleUnexpectedError);
        _searchCommand = new AsyncRelayCommand(() => LoadPageAsync(resetToFirstPage: true), onError: HandleUnexpectedError);
        _nextPageCommand = new AsyncRelayCommand(ExecuteNextPageAsync, () => CanGoNext && !IsBusy, HandleUnexpectedError);
        _previousPageCommand = new AsyncRelayCommand(ExecutePreviousPageAsync, () => CanGoPrevious && !IsBusy, HandleUnexpectedError);
        _newProductCommand = new AsyncRelayCommand(ExecuteNewProductAsync);
        _editProductCommand = new AsyncRelayCommand<ProductCatalogItem>(
            ExecuteEditProductAsync, item => item is not null && !IsBusy, HandleUnexpectedError);

        Products = new ObservableCollection<ProductCatalogItem>();
    }

    // El ViewModel nunca abre ventanas: solo pide abrir CreateProductWindow/EditProductWindow. El
    // shell reenvía el evento hasta App.xaml.cs, igual que en SalesViewModel.
    public event EventHandler? NewProductRequested;

    public event EventHandler<ProductId>? EditProductRequested;

    public ICommand LoadCommand => _loadCommand;

    public ICommand SearchCommand => _searchCommand;

    public ICommand NextPageCommand => _nextPageCommand;

    public ICommand PreviousPageCommand => _previousPageCommand;

    public ICommand NewProductCommand => _newProductCommand;

    public ICommand EditProductCommand => _editProductCommand;

    public ObservableCollection<ProductCatalogItem> Products { get; }

    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

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
    public Task RefreshAsync() => LoadPageAsync(resetToFirstPage: true);

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

    private async Task LoadPageAsync(bool resetToFirstPage)
    {
        if (resetToFirstPage)
        {
            CurrentPage = 1;
        }

        IsBusy = true;

        try
        {
            var skip = (CurrentPage - 1) * PageSize;
            var result = await _productManagementService.GetCatalogPageAsync(SearchText, SelectedFilter, skip, PageSize);

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

            // Selección automática cuando la búsqueda resulta en un único producto: cubre tanto
            // "buscar por SKU exacto" como el refresco tras crear/editar un producto.
            SelectedProduct = Products.Count == 1 ? Products[0] : null;

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

    private void HandleUnexpectedError(Exception exception) => GeneralError = "Ocurrió un error inesperado.";
}
