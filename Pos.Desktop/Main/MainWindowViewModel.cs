using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using Pos.Application.Authentication;
using Pos.Application.Products.ManageProduct;
using Pos.Application.RegisterSessions;
using Pos.Application.SalesCart;
using Pos.Desktop.Audit.Products;
using Pos.Desktop.Common;
using Pos.Desktop.Dashboard;
using Pos.Desktop.Inventory;
using Pos.Desktop.Products.Catalog;
using Pos.Desktop.Register;
using Pos.Desktop.Sales;
using Pos.Desktop.Settings;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;

namespace Pos.Desktop.Main;

// Shell de la aplicación (TAREA 24C): mantiene únicamente estado global (sesión, caja,
// navegación) y coordina logout/cerrar caja/nuevo producto/editar producto entre las páginas
// hijas. La lógica de presentación de cada página vive en su propio ViewModel
// (SalesViewModel/ProductsViewModel/InventoryViewModel/RegisterViewModel/SettingsViewModel), ya
// construido e inyectado aquí: MainWindowViewModel nunca resuelve servicios ni ventanas por sí
// mismo (sin Service Locator).
public sealed class MainWindowViewModel : ViewModelBase
{
    private const double SidebarExpandedWidth = 240d;
    private const double SidebarCollapsedWidth = 68d;

    private readonly ICurrentUserSession _session;
    private readonly ICurrentRegisterSession _registerSession;
    private readonly ICurrentSalesCart _currentSalesCart;
    private readonly DashboardViewModel _dashboardViewModel;
    private readonly SalesViewModel _salesViewModel;
    private readonly ProductsViewModel _productsViewModel;
    private readonly InventoryViewModel _inventoryViewModel;
    private readonly RegisterViewModel _registerViewModel;
    private readonly SettingsViewModel _settingsViewModel;
    private readonly ProductAuditViewModel _productAuditViewModel;
    private readonly AsyncRelayCommand _logoutCommand;
    private readonly AsyncRelayCommand _closeRegisterCommand;
    private readonly AsyncRelayCommand _toggleSidebarCommand;

    // Solo Auditoría tiene submenú hoy (TAREA 24D, sección 23): un HashSet generaliza sin
    // introducir una estructura de árbol completa que ningún otro ítem necesita todavía.
    private readonly HashSet<NavigationSection> _expandedSections = [];
    private IReadOnlyList<NavigationItem> _topLevelNavigationItems = Array.Empty<NavigationItem>();

    private string? _logoutBlockedMessage;
    private string? _closeRegisterBlockedMessage;
    private object _currentViewModel;
    private NavigationItem? _selectedNavigationItem;
    private bool _isSidebarExpanded = true;

    // ProductId del producto para el que se pidió edición: recordado para poder reenviar
    // ApplyProductUpdated al ViewModel que originó la solicitud (Venta o Productos), sin que el
    // shell necesite saber cuál está activo en ese momento.
    private object? _pendingNewProductSource;
    private object? _pendingEditProductSource;

    public MainWindowViewModel(
        ICurrentUserSession session,
        ICurrentRegisterSession registerSession,
        ICurrentSalesCart currentSalesCart,
        DashboardViewModel dashboardViewModel,
        SalesViewModel salesViewModel,
        ProductsViewModel productsViewModel,
        InventoryViewModel inventoryViewModel,
        RegisterViewModel registerViewModel,
        SettingsViewModel settingsViewModel,
        ProductAuditViewModel productAuditViewModel)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _registerSession = registerSession ?? throw new ArgumentNullException(nameof(registerSession));
        _currentSalesCart = currentSalesCart ?? throw new ArgumentNullException(nameof(currentSalesCart));
        _dashboardViewModel = dashboardViewModel ?? throw new ArgumentNullException(nameof(dashboardViewModel));
        _salesViewModel = salesViewModel ?? throw new ArgumentNullException(nameof(salesViewModel));
        _productsViewModel = productsViewModel ?? throw new ArgumentNullException(nameof(productsViewModel));
        _inventoryViewModel = inventoryViewModel ?? throw new ArgumentNullException(nameof(inventoryViewModel));
        _registerViewModel = registerViewModel ?? throw new ArgumentNullException(nameof(registerViewModel));
        _settingsViewModel = settingsViewModel ?? throw new ArgumentNullException(nameof(settingsViewModel));
        _productAuditViewModel = productAuditViewModel ?? throw new ArgumentNullException(nameof(productAuditViewModel));

        _logoutCommand = new AsyncRelayCommand(ExecuteLogoutAsync);
        _closeRegisterCommand = new AsyncRelayCommand(ExecuteCloseRegisterAsync);
        _toggleSidebarCommand = new AsyncRelayCommand(ExecuteToggleSidebarAsync);

        _dashboardViewModel.NavigateToProductsRequested += OnDashboardNavigateToProductsRequested;
        _salesViewModel.NewProductRequested += OnSalesNewProductRequested;
        _salesViewModel.EditProductRequested += OnSalesEditProductRequested;
        _salesViewModel.CheckoutRequested += OnSalesCheckoutRequested;
        _productsViewModel.NewProductRequested += OnProductsNewProductRequested;
        _productsViewModel.EditProductRequested += OnProductsEditProductRequested;
        _productsViewModel.AuditRequested += OnProductsAuditRequested;
        _registerViewModel.CloseRegisterRequested += OnRegisterViewCloseRegisterRequested;

        _topLevelNavigationItems = BuildNavigationItems().ToList();
        NavigationItems = new ObservableCollection<NavigationItem>();
        RebuildVisibleNavigationItems();
        _currentViewModel = _dashboardViewModel;

        if (NavigationItems.FirstOrDefault() is { } firstItem)
        {
            _selectedNavigationItem = firstItem;
            ApplySection(firstItem.Section);
        }
    }

    public event EventHandler? LogoutRequested;

    public event EventHandler? CloseRegisterRequested;

    // El ViewModel nunca abre ventanas: solo pide abrir CreateProductWindow/EditProductWindow. El
    // código detrás de MainWindow reenvía el evento hasta App.xaml.cs, único lugar que resuelve
    // ventanas desde el contenedor de DI.
    public event EventHandler? NewProductRequested;

    public event EventHandler<ProductId>? EditProductRequested;

    // Igual patrón que NewProductRequested/EditProductRequested, pero para CheckoutWindow.
    public event EventHandler? CheckoutRequested;

    public ICommand LogoutCommand => _logoutCommand;

    public ICommand CloseRegisterCommand => _closeRegisterCommand;

    public ICommand ToggleSidebarCommand => _toggleSidebarCommand;

    // Alternar el sidebar nunca recrea vistas, ViewModels ni limpia el carrito: solo cambia el
    // ancho de la columna del sidebar (TAREA 24C.1, sección 4).
    public bool IsSidebarExpanded
    {
        get => _isSidebarExpanded;
        private set
        {
            if (SetProperty(ref _isSidebarExpanded, value))
            {
                OnPropertyChanged(nameof(SidebarWidth));
            }
        }
    }

    public double SidebarWidth => IsSidebarExpanded ? SidebarExpandedWidth : SidebarCollapsedWidth;

    public string CurrentNavigationTitle => SelectedNavigationItem?.Label ?? string.Empty;

    // Sesión ausente produce un estado seguro: cadenas vacías en lugar de excepción.
    public string DisplayName => _session.CurrentUser?.DisplayName ?? string.Empty;

    public string RoleName => _session.CurrentUser?.RoleName ?? string.Empty;

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

    // Items visibles según permisos del usuario actual (TAREA 24C, sección 23): un ítem sin
    // permiso simplemente no aparece en la lista, en vez de mostrarse deshabilitado.
    public ObservableCollection<NavigationItem> NavigationItems { get; }

    public NavigationItem? SelectedNavigationItem
    {
        get => _selectedNavigationItem;
        set
        {
            // Un ítem con hijos (Auditoría) nunca navega directamente: solo expande/colapsa su
            // submenú (TAREA 24D, sección 23). Se revierte la selección visual del ListBox
            // notificando el getter sin cambiar _selectedNavigationItem.
            if (value is not null && value.HasChildren)
            {
                ToggleExpanded(value.Section);
                OnPropertyChanged(nameof(SelectedNavigationItem));
                return;
            }

            if (SetProperty(ref _selectedNavigationItem, value))
            {
                OnPropertyChanged(nameof(CurrentNavigationTitle));

                if (value is not null)
                {
                    ApplySection(value.Section);
                }
            }
        }
    }

    // El ContentControl del shell se enlaza a esta propiedad; el DataTemplate correspondiente
    // (por tipo de ViewModel) decide qué UserControl mostrar. Cambiar de página nunca recrea los
    // ViewModels hijos: el carrito y el estado de cada página se conservan (TAREA 24C, sección 24).
    public object CurrentViewModel
    {
        get => _currentViewModel;
        private set => SetProperty(ref _currentViewModel, value);
    }

    private void ApplySection(NavigationSection section)
    {
        CurrentViewModel = section switch
        {
            NavigationSection.Dashboard => _dashboardViewModel,
            NavigationSection.Sales => _salesViewModel,
            NavigationSection.Products => _productsViewModel,
            NavigationSection.Inventory => _inventoryViewModel,
            NavigationSection.Register => _registerViewModel,
            NavigationSection.Settings => _settingsViewModel,
            NavigationSection.AuditProducts => _productAuditViewModel,
            _ => _dashboardViewModel,
        };

        // ProductsView/DashboardView pueden refrescar al entrar (TAREA 24C, sección 24 / TAREA
        // 24C.1, sección 10): se disparan a través del ICommand (no llamando al método async
        // directamente) para reutilizar el manejo de errores de AsyncRelayCommand.
        if (section == NavigationSection.Products && _productsViewModel.LoadCommand.CanExecute(null))
        {
            _productsViewModel.LoadCommand.Execute(null);
        }

        if (section == NavigationSection.Dashboard && _dashboardViewModel.LoadCommand.CanExecute(null))
        {
            _dashboardViewModel.LoadCommand.Execute(null);
        }

        if (section == NavigationSection.AuditProducts && _productAuditViewModel.LoadCommand.CanExecute(null))
        {
            _productAuditViewModel.LoadCommand.Execute(null);
        }
    }

    // Único ítem con submenú hoy (Auditoría): expandir/colapsar reconstruye la lista visible
    // insertando los hijos justo después de su padre (TAREA 24D, sección 23).
    private void ToggleExpanded(NavigationSection section)
    {
        if (!_expandedSections.Remove(section))
        {
            _expandedSections.Add(section);
        }

        RebuildVisibleNavigationItems();
    }

    private void RebuildVisibleNavigationItems()
    {
        NavigationItems.Clear();

        foreach (var item in _topLevelNavigationItems)
        {
            NavigationItems.Add(item);

            if (item.HasChildren && _expandedSections.Contains(item.Section))
            {
                foreach (var child in item.Children!)
                {
                    NavigationItems.Add(child);
                }
            }
        }
    }

    private Task ExecuteToggleSidebarAsync()
    {
        IsSidebarExpanded = !IsSidebarExpanded;

        return Task.CompletedTask;
    }

    private void OnDashboardNavigateToProductsRequested(object? sender, EventArgs e)
    {
        if (NavigationItems.FirstOrDefault(i => i.Section == NavigationSection.Products) is { } productsItem)
        {
            SelectedNavigationItem = productsItem;
        }
    }

    private IEnumerable<NavigationItem> BuildNavigationItems()
    {
        var user = _session.CurrentUser;

        if (user is null)
        {
            yield break;
        }

        // Dashboard es visible para todo usuario autenticado, sin gate de permiso específico
        // (TAREA 24C.1, sección 12): las tarjetas sensibles dentro del propio Dashboard sí
        // respetan ManageProducts (ver DashboardViewModel.CanViewProductCards).
        yield return new NavigationItem(NavigationSection.Dashboard, "Dashboard");

        if (user.HasPermission(Permission.ProcessSale))
        {
            yield return new NavigationItem(NavigationSection.Sales, "Venta");
        }

        if (user.HasPermission(Permission.ManageProducts))
        {
            yield return new NavigationItem(NavigationSection.Products, "Productos");
        }

        if (user.HasPermission(Permission.ManageProducts) || user.HasPermission(Permission.AdjustInventory))
        {
            yield return new NavigationItem(NavigationSection.Inventory, "Inventario");
        }

        if (user.HasPermission(Permission.OpenRegisterSession) || user.HasPermission(Permission.CloseRegisterSession))
        {
            yield return new NavigationItem(NavigationSection.Register, "Caja");
        }

        // No existe un permiso administrativo genérico en Domain.Security.Permission: se usa
        // ManageUsers (el permiso administrativo real más cercano) en vez de inventar uno nuevo o
        // comparar RoleName=="Administrator" (TAREA 24C, sección 23). Reportado como decisión.
        if (user.HasPermission(Permission.ManageUsers))
        {
            yield return new NavigationItem(NavigationSection.Settings, "Configuración");
        }

        // Auditoría > Productos: visible únicamente con ViewProductAudit, nunca por RoleName
        // (TAREA 24D, sección 16/23).
        if (user.HasPermission(Permission.ViewProductAudit))
        {
            yield return new NavigationItem(NavigationSection.Audit, "Auditoría",
            [
                new NavigationItem(NavigationSection.AuditProducts, "Productos"),
            ]);
        }
    }

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
        if (_currentSalesCart.Snapshot.HasItems)
        {
            CloseRegisterBlockedMessage = "Cancela la venta actual antes de cerrar la caja.";
            return Task.CompletedTask;
        }

        CloseRegisterBlockedMessage = null;
        CloseRegisterRequested?.Invoke(this, EventArgs.Empty);

        return Task.CompletedTask;
    }

    private void OnRegisterViewCloseRegisterRequested(object? sender, EventArgs e) =>
        _closeRegisterCommand.Execute(null);

    private void OnSalesNewProductRequested(object? sender, EventArgs e)
    {
        _pendingNewProductSource = _salesViewModel;
        NewProductRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnProductsNewProductRequested(object? sender, EventArgs e)
    {
        _pendingNewProductSource = _productsViewModel;
        NewProductRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnSalesEditProductRequested(object? sender, ProductId productId)
    {
        _pendingEditProductSource = _salesViewModel;
        EditProductRequested?.Invoke(this, productId);
    }

    private void OnSalesCheckoutRequested(object? sender, EventArgs e) =>
        CheckoutRequested?.Invoke(this, EventArgs.Empty);

    private void OnProductsEditProductRequested(object? sender, ProductId productId)
    {
        _pendingEditProductSource = _productsViewModel;
        EditProductRequested?.Invoke(this, productId);
    }

    // "Ver detalle" desde el indicador de actividad reciente (TAREA 24D, sección 32/33): preaplica
    // el filtro por ProductId en ProductAuditViewModel antes de navegar, expandiendo Auditoría si
    // hiciera falta. No abre ninguna ventana nueva.
    private void OnProductsAuditRequested(object? sender, ProductCatalogItem item)
    {
        _productAuditViewModel.ApplyProductFilter(item.ProductId, item.Sku);

        if (_expandedSections.Add(NavigationSection.Audit))
        {
            RebuildVisibleNavigationItems();
        }

        if (NavigationItems.FirstOrDefault(i => i.Section == NavigationSection.AuditProducts) is { } auditProductsItem)
        {
            SelectedNavigationItem = auditProductsItem;
        }
    }

    // Llamado desde App.xaml.cs tras crear un producto exitosamente en CreateProductWindow:
    // reenvía al ViewModel que originó la solicitud (Venta o Productos).
    public void ApplyProductCreated(string sku)
    {
        switch (_pendingNewProductSource)
        {
            case SalesViewModel:
                _salesViewModel.ApplyProductCreated(sku);
                break;
            case ProductsViewModel:
                _productsViewModel.ApplyProductCreated(sku);
                break;
        }
    }

    // Llamado desde App.xaml.cs tras cerrar EditProductWindow (edición, activar/desactivar o
    // ajuste de inventario): reenvía al ViewModel que originó la solicitud.
    public void ApplyProductUpdated(string sku)
    {
        switch (_pendingEditProductSource)
        {
            case SalesViewModel:
                _salesViewModel.ApplyProductUpdated(sku);
                break;
            case ProductsViewModel:
                _productsViewModel.ApplyProductUpdated(sku);
                break;
        }
    }

    // Llamado desde App.xaml.cs tras cerrar CheckoutWindow con un cobro exitoso. El checkout solo
    // puede iniciarse desde Venta, así que siempre reenvía a _salesViewModel (sin necesidad de
    // rastrear un origen pendiente, a diferencia de ApplyProductCreated/ApplyProductUpdated).
    public void ApplyCheckoutCompleted() => _salesViewModel.ApplyCheckoutCompleted();
}
