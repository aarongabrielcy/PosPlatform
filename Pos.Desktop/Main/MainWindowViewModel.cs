using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using Pos.Application.AdministrativeNotifications;
using Pos.Application.Authentication;
using Pos.Application.Enforcement;
using Pos.Application.Inventory;
using Pos.Application.Products.ManageProduct;
using Pos.Application.RegisterSessions;
using Pos.Application.SalesCart;
using Pos.Desktop.AdministrativeNotifications;
using Pos.Desktop.Audit.Products;
using Pos.Desktop.Common;
using Pos.Desktop.Dashboard;
using Pos.Desktop.Inventory;
using Pos.Desktop.LocalConfiguration;
using Pos.Desktop.Products.Catalog;
using Pos.Desktop.Register;
using Pos.Desktop.Reports;
using Pos.Desktop.Sales;
using Pos.Desktop.Sales.History;
using Pos.Desktop.Users;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;

namespace Pos.Desktop.Main;

// Shell de la aplicación (TAREA 24C): mantiene únicamente estado global (sesión, caja,
// navegación) y coordina logout/cerrar caja/nuevo producto/editar producto entre las páginas
// hijas. La lógica de presentación de cada página vive en su propio ViewModel
// (SalesViewModel/ProductsViewModel/InventoryViewModel/RegisterViewModel/SettingsViewModel), ya
// construido e inyectado aquí: MainWindowViewModel nunca resuelve servicios ni ventanas por sí
// mismo (sin Service Locator).
public sealed class MainWindowViewModel : ViewModelBase, IDisposable
{
    private const double SidebarExpandedWidth = 240d;
    private const double SidebarCollapsedWidth = 68d;

    private readonly ICurrentUserSession _session;
    private readonly ICurrentRegisterSession _registerSession;
    private readonly ICurrentSalesCart _currentSalesCart;
    private readonly IInstallationEnforcementStateService _enforcementStateService;

    // Capturado en el hilo que construye este ViewModel (el hilo de UI en producción — ver
    // App.xaml.cs ShowMainWindow) en vez de depender de System.Windows.Application.Current: evita
    // que un evento StateChanged disparado por InstallationHeartbeatBackgroundService (hilo
    // distinto) se pierda silenciosamente, y permite probar el marshaling sin levantar una
    // System.Windows.Application real en las pruebas (sección 38 de la tarea: "evitar automatización
    // WPF pesada").
    private readonly System.Windows.Threading.Dispatcher _dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
    private readonly DashboardViewModel _dashboardViewModel;
    private readonly SalesViewModel _salesViewModel;
    private readonly SalesHistoryViewModel _salesHistoryViewModel;
    private readonly ProductsViewModel _productsViewModel;
    private readonly InventoryViewModel _inventoryViewModel;
    private readonly RegisterViewModel _registerViewModel;
    private readonly UserManagementViewModel _userManagementViewModel;
    private readonly ProductAuditViewModel _productAuditViewModel;
    private readonly ReportsViewModel _reportsViewModel;
    private readonly LocalConfigurationViewModel _localConfigurationViewModel;
    private readonly NotificationCenterViewModel _notificationCenterViewModel;
    private readonly AsyncRelayCommand _logoutCommand;
    private readonly AsyncRelayCommand _closeRegisterCommand;
    private readonly AsyncRelayCommand _toggleSidebarCommand;

    // Solo Auditoría tiene submenú hoy (TAREA 24D, sección 23): un HashSet generaliza sin
    // introducir una estructura de árbol completa que ningún otro ítem necesita todavía.
    private readonly HashSet<NavigationSection> _expandedSections = [];
    private IReadOnlyList<NavigationItem> _topLevelNavigationItems = Array.Empty<NavigationItem>();

    private string? _logoutBlockedMessage;
    private string? _closeRegisterBlockedMessage;
    private string? _enforcementBannerText;
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
        IInstallationEnforcementStateService enforcementStateService,
        DashboardViewModel dashboardViewModel,
        SalesViewModel salesViewModel,
        SalesHistoryViewModel salesHistoryViewModel,
        ProductsViewModel productsViewModel,
        InventoryViewModel inventoryViewModel,
        RegisterViewModel registerViewModel,
        UserManagementViewModel userManagementViewModel,
        ProductAuditViewModel productAuditViewModel,
        ReportsViewModel reportsViewModel,
        LocalConfigurationViewModel localConfigurationViewModel,
        NotificationCenterViewModel notificationCenterViewModel)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _registerSession = registerSession ?? throw new ArgumentNullException(nameof(registerSession));
        _currentSalesCart = currentSalesCart ?? throw new ArgumentNullException(nameof(currentSalesCart));
        _enforcementStateService = enforcementStateService ?? throw new ArgumentNullException(nameof(enforcementStateService));
        _dashboardViewModel = dashboardViewModel ?? throw new ArgumentNullException(nameof(dashboardViewModel));
        _salesViewModel = salesViewModel ?? throw new ArgumentNullException(nameof(salesViewModel));
        _salesHistoryViewModel = salesHistoryViewModel ?? throw new ArgumentNullException(nameof(salesHistoryViewModel));
        _productsViewModel = productsViewModel ?? throw new ArgumentNullException(nameof(productsViewModel));
        _inventoryViewModel = inventoryViewModel ?? throw new ArgumentNullException(nameof(inventoryViewModel));
        _registerViewModel = registerViewModel ?? throw new ArgumentNullException(nameof(registerViewModel));
        _userManagementViewModel = userManagementViewModel ?? throw new ArgumentNullException(nameof(userManagementViewModel));
        _productAuditViewModel = productAuditViewModel ?? throw new ArgumentNullException(nameof(productAuditViewModel));
        _reportsViewModel = reportsViewModel ?? throw new ArgumentNullException(nameof(reportsViewModel));
        _localConfigurationViewModel = localConfigurationViewModel ?? throw new ArgumentNullException(nameof(localConfigurationViewModel));
        _notificationCenterViewModel = notificationCenterViewModel ?? throw new ArgumentNullException(nameof(notificationCenterViewModel));

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
        _inventoryViewModel.AdjustInventoryRequested += OnInventoryAdjustInventoryRequested;
        _registerViewModel.CloseRegisterRequested += OnRegisterViewCloseRegisterRequested;
        _registerViewModel.CashInRequested += OnRegisterViewCashInRequested;
        _registerViewModel.CashOutRequested += OnRegisterViewCashOutRequested;
        _userManagementViewModel.NewUserRequested += OnUserManagementNewUserRequested;
        _userManagementViewModel.EditUserRequested += OnUserManagementEditUserRequested;
        _notificationCenterViewModel.OpenNotificationRequested += OnNotificationCenterOpenNotificationRequested;
        _enforcementStateService.StateChanged += OnEnforcementStateChanged;

        UpdateEnforcementBanner(_enforcementStateService.Current);

        _topLevelNavigationItems = BuildNavigationItems().ToList();
        NavigationItems = new ObservableCollection<NavigationItem>();
        RebuildVisibleNavigationItems();
        _currentViewModel = _dashboardViewModel;

        if (NavigationItems.FirstOrDefault() is { } firstItem)
        {
            _selectedNavigationItem = firstItem;
            ApplySection(firstItem.Section);
        }

        // Badge inicial al construir el shell (TAREA 24E, sección 32): sin polling, se dispara una
        // sola vez aquí; RefreshCommand contiene su propio manejo de errores.
        if (_notificationCenterViewModel.RefreshCommand.CanExecute(null))
        {
            _notificationCenterViewModel.RefreshCommand.Execute(null);
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

    // Igual patrón que EditProductRequested, pero para AdjustInventoryWindow abierto directamente
    // desde Inventario (TAREA 24G, sección 16/17): un solo origen posible (InventoryViewModel), sin
    // necesidad de rastrear un "pending source" como en New/EditProductRequested.
    public event EventHandler<InventoryCatalogItem>? AdjustInventoryRequested;

    // Igual patrón que New/EditProductRequested, pero para CreateUserWindow/EditUserWindow.
    // Usuarios tiene un único origen posible (esta pantalla), así que no necesita rastrear un
    // "pending source" como New/EditProductRequested.
    public event EventHandler? NewUserRequested;

    public event EventHandler<UserId>? EditUserRequested;

    // Igual patrón que CloseRegisterRequested, pero para RecordCashMovementWindow (BASIC-CASH-01,
    // sección 19-20): un único origen posible (RegisterViewModel).
    public event EventHandler? CashInRequested;

    public event EventHandler? CashOutRequested;

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

    // Aviso visible mientras la app sigue abierta y la instalación pasa a tener una restricción de
    // enforcement confirmada (sección 17/38 de la tarea): la guarda de Application ya bloquea las
    // mutaciones por sí sola; este banner solo hace visible el motivo sin exigir reiniciar la app.
    public string? EnforcementBannerText
    {
        get => _enforcementBannerText;
        private set
        {
            if (SetProperty(ref _enforcementBannerText, value))
            {
                OnPropertyChanged(nameof(HasEnforcementBanner));
            }
        }
    }

    public bool HasEnforcementBanner => !string.IsNullOrEmpty(_enforcementBannerText);

    // Items visibles según permisos del usuario actual (TAREA 24C, sección 23): un ítem sin
    // permiso simplemente no aparece en la lista, en vez de mostrarse deshabilitado.
    public ObservableCollection<NavigationItem> NavigationItems { get; }

    // El panel del centro de notificaciones se enlaza directamente a este ViewModel hijo (TAREA
    // 24E, sección 31), igual patrón que CurrentViewModel expone las páginas del shell.
    public NotificationCenterViewModel NotificationCenterViewModel => _notificationCenterViewModel;

    // La campana es visible únicamente con ViewProductAudit (TAREA 24E, sección 24): un usuario
    // sin el permiso ni siquiera ve el ícono.
    public bool CanViewNotifications => _session.CurrentUser?.HasPermission(Permission.ViewProductAudit) ?? false;

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
            NavigationSection.SalesPointOfSale => _salesViewModel,
            NavigationSection.SalesHistory => _salesHistoryViewModel,
            NavigationSection.Products => _productsViewModel,
            NavigationSection.Inventory => _inventoryViewModel,
            NavigationSection.Register => _registerViewModel,
            NavigationSection.Settings => _userManagementViewModel,
            NavigationSection.AuditProducts => _productAuditViewModel,
            NavigationSection.Reports => _reportsViewModel,
            NavigationSection.LocalConfiguration => _localConfigurationViewModel,
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

        if (section == NavigationSection.Inventory && _inventoryViewModel.LoadCommand.CanExecute(null))
        {
            _inventoryViewModel.LoadCommand.Execute(null);
        }

        // Refresca el historial de movimientos de caja al entrar a Caja (BASIC-CASH-01, sección
        // 21): sin cache permanente, mismo patrón que el resto de LoadCommand por sección.
        if (section == NavigationSection.Register && _registerViewModel.RefreshMovementsCommand.CanExecute(null))
        {
            _registerViewModel.RefreshMovementsCommand.Execute(null);
        }

        // Consulta la DB al entrar a Historial (TAREA 25B, sección 33): sin cache permanente, sin
        // polling. Preserva filtros/página ya elegidos (ver SalesHistoryViewModel.LoadCommand).
        if (section == NavigationSection.SalesHistory && _salesHistoryViewModel.LoadCommand.CanExecute(null))
        {
            _salesHistoryViewModel.LoadCommand.Execute(null);
        }

        // Consulta la lista de usuarios al entrar a Usuarios (BASIC-USR-01, igual patrón que
        // Productos/Historial): sin cache permanente, sin polling.
        if (section == NavigationSection.Settings && _userManagementViewModel.LoadCommand.CanExecute(null))
        {
            _userManagementViewModel.LoadCommand.Execute(null);
        }

        // Consulta las 6 áreas de Reportes al entrar (BASIC-RPT-01, igual patrón que
        // Historial/Inventario/Usuarios): sin cache permanente, sin polling.
        if (section == NavigationSection.Reports && _reportsViewModel.LoadCommand.CanExecute(null))
        {
            _reportsViewModel.LoadCommand.Execute(null);
        }

        // BASIC-CFG-01: recarga impresoras instaladas/valores efectivos al entrar a Configuración,
        // mismo patrón que el resto de secciones (sin cache permanente, sin polling).
        if (section == NavigationSection.LocalConfiguration && _localConfigurationViewModel.LoadCommand.CanExecute(null))
        {
            _localConfigurationViewModel.LoadCommand.Execute(null);
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

        // Ventas > Punto de venta / Historial (TAREA 25B, sección 9): mismo patrón padre/hijo que
        // Auditoría. El padre "Ventas" es visible si el usuario puede acceder al menos a uno de sus
        // hijos; cada hijo aparece según su propio permiso, nunca por Role.Name.
        var salesChildren = new List<NavigationItem>();

        if (user.HasPermission(Permission.ProcessSale))
        {
            salesChildren.Add(new NavigationItem(NavigationSection.SalesPointOfSale, "Punto de venta"));
        }

        // READ-ONLY CORRECTION: Historial/Productos/Inventario ahora se gatean con sus propios
        // permisos de lectura (ViewSalesHistory/ViewProducts/ViewInventory), separados de
        // ViewReports/ManageProducts/AdjustInventory (sección 14/15 de la tarea: Sales History no es
        // lo mismo que Administrative Reports, y READ != MODIFY). Manager/Administrator reciben
        // estos permisos de lectura además de los de administración (ver StandardRoles/
        // AdministrativePermissionSet), así que su visibilidad no cambia.
        if (user.HasPermission(Permission.ViewSalesHistory))
        {
            salesChildren.Add(new NavigationItem(NavigationSection.SalesHistory, "Historial"));
        }

        if (salesChildren.Count > 0)
        {
            yield return new NavigationItem(NavigationSection.Sales, "Ventas", salesChildren);
        }

        if (user.HasPermission(Permission.ViewProducts))
        {
            yield return new NavigationItem(NavigationSection.Products, "Productos");
        }

        if (user.HasPermission(Permission.ViewInventory))
        {
            yield return new NavigationItem(NavigationSection.Inventory, "Inventario");
        }

        if (user.HasPermission(Permission.OpenRegisterSession) || user.HasPermission(Permission.CloseRegisterSession))
        {
            yield return new NavigationItem(NavigationSection.Register, "Caja");
        }

        // Gate: Permission.ManageUsers (TAREA 24C, sección 23 - decisión original de reutilizar
        // este permiso ya existente en vez de inventar uno nuevo o comparar RoleName=="Administrator").
        // BASIC-USR-01: esta sección ahora contiene la administración real de usuarios, en vez del
        // placeholder "Configuración" anterior.
        if (user.HasPermission(Permission.ManageUsers))
        {
            yield return new NavigationItem(NavigationSection.Settings, "Usuarios");
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

        // Reportes (BASIC-RPT-01, sección 6): visible únicamente con ViewReports. Manager/
        // Administrator lo reciben (StandardRoles/AdministrativePermissionSet); Cashier nunca
        // (sección 29 de la tarea).
        if (user.HasPermission(Permission.ViewReports))
        {
            yield return new NavigationItem(NavigationSection.Reports, "Reportes");
        }

        // BASIC-CFG-01, sección 27/28: Configuración es funcionalidad administrativa sensible,
        // gateada por Permission.ManageSettings - nunca por nombre de Role. Administrator la recibe
        // automáticamente vía AdministrativePermissionSet.All(); Manager/Cashier nunca (no están en
        // StandardRoles.ManagerPermissions()/CashierPermissions()). Ítem de navegación separado de
        // "Usuarios" (NavigationSection.Settings, gateado por ManageUsers).
        if (user.HasPermission(Permission.ManageSettings))
        {
            yield return new NavigationItem(NavigationSection.LocalConfiguration, "Configuración");
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

    private void OnRegisterViewCashInRequested(object? sender, EventArgs e) =>
        CashInRequested?.Invoke(this, EventArgs.Empty);

    private void OnRegisterViewCashOutRequested(object? sender, EventArgs e) =>
        CashOutRequested?.Invoke(this, EventArgs.Empty);

    private void OnUserManagementNewUserRequested(object? sender, EventArgs e) =>
        NewUserRequested?.Invoke(this, EventArgs.Empty);

    private void OnUserManagementEditUserRequested(object? sender, UserId userId) =>
        EditUserRequested?.Invoke(this, userId);

    private void OnInventoryAdjustInventoryRequested(object? sender, InventoryCatalogItem item) =>
        AdjustInventoryRequested?.Invoke(this, item);

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

    // Click en una Notification (TAREA 24E, sección 29/30): NotificationCenterViewModel ya marcó
    // el receipt como leído y refrescó su propio badge antes de emitir este evento; aquí solo
    // falta navegar a Auditoría > Productos y seleccionar exactamente ese AuditEvent (nunca "el
    // más reciente del producto"). Igual patrón que OnProductsAuditRequested, pero con el filtro
    // exacto por AuditEventId.
    private void OnNotificationCenterOpenNotificationRequested(object? sender, AdministrativeNotificationItem item)
    {
        _productAuditViewModel.ApplyAuditEventFilter(item.ProductId, item.ProductSku, item.AuditEventId);

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

        RefreshNotificationBadge();
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

        RefreshNotificationBadge();
    }

    // Llamado desde App.xaml.cs tras cerrar CheckoutWindow con un cobro exitoso. El checkout solo
    // puede iniciarse desde Venta, así que siempre reenvía a _salesViewModel (sin necesidad de
    // rastrear un origen pendiente, a diferencia de ApplyProductCreated/ApplyProductUpdated).
    public void ApplyCheckoutCompleted() => _salesViewModel.ApplyCheckoutCompleted();

    // Llamado desde App.xaml.cs tras cerrar AdjustInventoryWindow abierto directamente desde
    // Inventario (a diferencia del flujo de Productos, aquí no hay EditProductWindow de por medio):
    // refresca Existencias/KPIs/Movimientos y el badge de notificaciones (TAREA 24G, sección 19).
    public void ApplyInventoryAdjusted()
    {
        _inventoryViewModel.ApplyInventoryAdjusted();
        RefreshNotificationBadge();
    }

    // Llamado desde App.xaml.cs tras cerrar CreateUserWindow/EditUserWindow con al menos un cambio
    // aplicado (BASIC-USR-01): refresca la lista de usuarios, igual patrón que
    // ApplyProductCreated/ApplyProductUpdated.
    public void ApplyUserChanged()
    {
        if (_userManagementViewModel.LoadCommand.CanExecute(null))
        {
            _userManagementViewModel.LoadCommand.Execute(null);
        }
    }

    // Llamado desde App.xaml.cs tras cerrar RecordCashMovementWindow con un movimiento registrado
    // con éxito (BASIC-CASH-01, sección 20-21): refresca el historial de movimientos de Caja.
    public void ApplyCashMovementRecorded()
    {
        if (_registerViewModel.RefreshMovementsCommand.CanExecute(null))
        {
            _registerViewModel.RefreshMovementsCommand.Execute(null);
        }
    }

    // Una operación Product sensible (editar, activar/desactivar, ajustar inventario) puede haber
    // generado una AdministrativeNotification; el badge debe reflejarlo sin reiniciar la app
    // (TAREA 24E, sección 32/33). Desktop nunca decide "esto notifica o no": solo pide refrescar
    // el conteo ya calculado por Application.
    private void RefreshNotificationBadge()
    {
        if (_notificationCenterViewModel.RefreshCommand.CanExecute(null))
        {
            _notificationCenterViewModel.RefreshCommand.Execute(null);
        }
    }

    private void OnEnforcementStateChanged(object? sender, InstallationEnforcementState state)
    {
        // Se dispara desde InstallationHeartbeatBackgroundService (hilo distinto al de UI): los
        // bindings de WPF exigen que los cambios lleguen desde el hilo de UI.
        _dispatcher.Invoke(() => UpdateEnforcementBanner(state));
    }

    private void UpdateEnforcementBanner(InstallationEnforcementState state)
    {
        EnforcementBannerText = state switch
        {
            InstallationEnforcementState.Suspended =>
                "La instalación está suspendida. Las operaciones nuevas están restringidas hasta que se resuelva.",
            InstallationEnforcementState.CredentialInvalid =>
                "La credencial de esta instalación ya no es válida. Las operaciones nuevas están restringidas.",
            InstallationEnforcementState.Decommissioned =>
                "Esta instalación fue dada de baja. Las operaciones nuevas están restringidas.",
            _ => null,
        };
    }

    public void Dispose() => _enforcementStateService.StateChanged -= OnEnforcementStateChanged;
}
