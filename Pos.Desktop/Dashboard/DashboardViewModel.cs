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

namespace Pos.Desktop.Dashboard;

// Vista inicial del shell tras iniciar sesión/abrir caja (TAREA 24C.1). Usa exclusivamente datos
// reales ya disponibles: catálogo (Total/Stock bajo/Sin existencia, vía IProductManagementService,
// que a su vez usa IProductCatalogQuery sin N+1), caja actual (ICurrentRegisterSession) y venta
// actual (ICurrentSalesCart). No muestra ventas del día, ingreso ni productos más vendidos: eso
// depende de que TAREA 25A complete el cobro.
public sealed class DashboardViewModel : ViewModelBase
{
    private const int LowStockPreviewSize = 10;

    private readonly ICurrentUserSession _currentUserSession;
    private readonly ICurrentRegisterSession _currentRegisterSession;
    private readonly ICurrentSalesCart _currentSalesCart;
    private readonly IProductManagementService _productManagementService;
    private readonly AsyncRelayCommand _loadCommand;
    private readonly AsyncRelayCommand<ProductCatalogItem> _viewProductCommand;

    private bool _isBusy;
    private string? _generalError;
    private int _totalProducts;
    private int _lowStockCount;
    private int _outOfStockCount;
    private string _cartTotalText = string.Empty;
    private int _cartLineCount;

    public DashboardViewModel(
        ICurrentUserSession currentUserSession,
        ICurrentRegisterSession currentRegisterSession,
        ICurrentSalesCart currentSalesCart,
        IProductManagementService productManagementService)
    {
        _currentUserSession = currentUserSession ?? throw new ArgumentNullException(nameof(currentUserSession));
        _currentRegisterSession = currentRegisterSession ?? throw new ArgumentNullException(nameof(currentRegisterSession));
        _currentSalesCart = currentSalesCart ?? throw new ArgumentNullException(nameof(currentSalesCart));
        _productManagementService = productManagementService ?? throw new ArgumentNullException(nameof(productManagementService));

        _loadCommand = new AsyncRelayCommand(ExecuteLoadAsync, onError: HandleUnexpectedError);
        _viewProductCommand = new AsyncRelayCommand<ProductCatalogItem>(ExecuteViewProductAsync, item => item is not null);

        LowStockItems = new ObservableCollection<ProductCatalogItem>();
    }

    // El ViewModel nunca navega por sí mismo: solo pide navegar a Productos (doble clic en la
    // tabla de stock bajo). El shell decide cómo resolverlo, igual que New/EditProductRequested.
    public event EventHandler? NavigateToProductsRequested;

    public ICommand LoadCommand => _loadCommand;

    public ICommand ViewProductCommand => _viewProductCommand;

    // Las tarjetas de catálogo (Total/Stock bajo/Sin existencia) requieren ManageProducts, igual
    // que ProductsView: el Dashboard es navegable para cualquier usuario autenticado, pero no
    // expone datos de catálogo a quien no tendría acceso a Productos.
    public bool CanViewProductCards => _currentUserSession.CurrentUser?.HasPermission(Permission.ManageProducts) ?? false;

    public bool IsRegisterOpen => _currentRegisterSession.IsOpen;

    public string RegisterName => _currentRegisterSession.Current?.RegisterName ?? string.Empty;

    public string RegisterStatusText => IsRegisterOpen ? "Abierta" : "Cerrada";

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public string? GeneralError
    {
        get => _generalError;
        private set => SetProperty(ref _generalError, value);
    }

    public int TotalProducts
    {
        get => _totalProducts;
        private set => SetProperty(ref _totalProducts, value);
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

    public string CartTotalText
    {
        get => _cartTotalText;
        private set => SetProperty(ref _cartTotalText, value);
    }

    public int CartLineCount
    {
        get => _cartLineCount;
        private set => SetProperty(ref _cartLineCount, value);
    }

    public ObservableCollection<ProductCatalogItem> LowStockItems { get; }

    public string LowStockStatusMessage => LowStockItems.Count == 0 ? "No hay productos con stock bajo." : string.Empty;

    private async Task ExecuteLoadAsync()
    {
        IsBusy = true;

        try
        {
            ApplyCartAndRegisterState();

            if (CanViewProductCards)
            {
                var summary = await _productManagementService.GetDashboardSummaryAsync();
                TotalProducts = summary.TotalProducts;
                LowStockCount = summary.LowStockCount;
                OutOfStockCount = summary.OutOfStockCount;

                var lowStockPage = await _productManagementService.GetCatalogPageAsync(
                    null, ProductCatalogStatusFilter.LowStock, 0, LowStockPreviewSize);

                LowStockItems.Clear();

                foreach (var item in lowStockPage.Items)
                {
                    LowStockItems.Add(item);
                }
            }
            else
            {
                TotalProducts = 0;
                LowStockCount = 0;
                OutOfStockCount = 0;
                LowStockItems.Clear();
            }

            OnPropertyChanged(nameof(LowStockStatusMessage));
            GeneralError = null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyCartAndRegisterState()
    {
        OnPropertyChanged(nameof(IsRegisterOpen));
        OnPropertyChanged(nameof(RegisterName));
        OnPropertyChanged(nameof(RegisterStatusText));

        var snapshot = _currentSalesCart.Snapshot;
        CartTotalText = $"{snapshot.TotalAmount.ToString("N2", CultureInfo.CurrentCulture)} {snapshot.Currency}";
        CartLineCount = snapshot.Lines.Count;
    }

    private Task ExecuteViewProductAsync(ProductCatalogItem? item)
    {
        if (item is not null)
        {
            NavigateToProductsRequested?.Invoke(this, EventArgs.Empty);
        }

        return Task.CompletedTask;
    }

    private void HandleUnexpectedError(Exception exception) => GeneralError = "Ocurrió un error inesperado.";
}
