using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.AdministrativeNotifications;
using Pos.Application.Authentication;
using Pos.Application.Enforcement;
using Pos.Application.ProductAudit;
using Pos.Application.Products.ManageProduct;
using Pos.Application.RegisterSessions;
using Pos.Application.SalesCart;
using Pos.Desktop.AdministrativeNotifications;
using Pos.Desktop.Audit.Products;
using Pos.Desktop.Dashboard;
using Pos.Desktop.Inventory;
using Pos.Desktop.LocalConfiguration;
using Pos.Desktop.Main;
using FakeInventoryService = Pos.Desktop.Tests.Inventory.FakeInventoryService;
using Pos.Desktop.Products.Catalog;
using Pos.Desktop.Register;
using Pos.Desktop.Reports;
using Pos.Desktop.Sales;
using Pos.Desktop.Sales.History;
using Pos.Desktop.Users;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.ProductAudit;
using Pos.Domain.Security;
using CatalogFakeProductManagementService = Pos.Desktop.Tests.Products.Catalog.FakeProductManagementService;
using FakeAdministrativeNotificationService = Pos.Desktop.Tests.AdministrativeNotifications.FakeAdministrativeNotificationService;
using FakeClock = Pos.Desktop.Tests.Sales.History.FakeClock;
using FakeSalesHistoryService = Pos.Desktop.Tests.Sales.History.FakeSalesHistoryService;
using FakeUserManagementService = Pos.Desktop.Tests.Users.FakeUserManagementService;

namespace Pos.Desktop.Tests.Main;

public class MainWindowViewModelTests
{
    private static readonly DateTimeOffset OpenedAtUtc = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ExposesDisplayNameAndRoleNameFromTheCurrentSession()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
        var viewModel = CreateViewModel(session: session);

        Assert.Equal("Ana Pérez", viewModel.DisplayName);
        Assert.Equal("Cajero", viewModel.RoleName);
    }

    // ---------- Fecha/hora local del header (BASIC-UX-01, sección 22-24/51) ----------

    [Fact]
    public void ExposesLocalDateAndTimeFormattedForSpanishMexico()
    {
        var clock = new Pos.Desktop.Tests.Common.FakeDesktopClock(new DateTime(2026, 8, 22, 18, 45, 0));
        var viewModel = CreateViewModel(clock: clock);

        Assert.Equal("22/08/2026", viewModel.CurrentDateText);
        Assert.Equal("18:45", viewModel.CurrentTimeText);
    }

    // ---------- Indicador de conectividad POS Cloud (BASIC-UX-01, sección 27-38/52/55) ----------

    [Fact]
    public void StartsCheckingCloudConnectivityBeforeAnyHeartbeatOutcomeIsKnown()
    {
        var connectivityStateService = new Enforcement.FakeInstallationConnectivityStateService();
        var viewModel = CreateViewModel(connectivityStateService: connectivityStateService);

        Assert.Equal(Pos.Application.Enforcement.InstallationConnectivityState.Checking, viewModel.CloudConnectivityState);
        Assert.Equal("Comprobando POS Cloud...", viewModel.CloudStatusText);
    }

    [Fact]
    public void ConnectedCloudStateShowsATruthfulControlPlaneMessage()
    {
        var connectivityStateService = new Enforcement.FakeInstallationConnectivityStateService();
        var viewModel = CreateViewModel(connectivityStateService: connectivityStateService);

        connectivityStateService.RaiseStateChanged(Pos.Application.Enforcement.InstallationConnectivityState.Connected);

        Assert.Equal("POS Cloud conectado", viewModel.CloudStatusText);
        Assert.DoesNotContain("sincroniz", viewModel.CloudStatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OfflineCloudStateStillCommunicatesLocalOperationIsAvailable()
    {
        var connectivityStateService = new Enforcement.FakeInstallationConnectivityStateService();
        var viewModel = CreateViewModel(connectivityStateService: connectivityStateService);

        connectivityStateService.RaiseStateChanged(Pos.Application.Enforcement.InstallationConnectivityState.Offline);

        Assert.Equal("Sin conexión · operación local disponible", viewModel.CloudStatusText);
    }

    [Fact]
    public void DisposingStopsReactingToFurtherConnectivityStateChanges()
    {
        var connectivityStateService = new Enforcement.FakeInstallationConnectivityStateService();
        var viewModel = CreateViewModel(connectivityStateService: connectivityStateService);

        viewModel.Dispose();
        connectivityStateService.RaiseStateChanged(Pos.Application.Enforcement.InstallationConnectivityState.Connected);

        Assert.Equal(Pos.Application.Enforcement.InstallationConnectivityState.Checking, viewModel.CloudConnectivityState);
    }

    // ---------- Aviso de enforcement en la app en ejecución (sección 17/38 de la tarea) ----------

    [Fact]
    public void StartsWithoutAnEnforcementBannerWhenAllowed()
    {
        var viewModel = CreateViewModel();

        Assert.False(viewModel.HasEnforcementBanner);
        Assert.Null(viewModel.EnforcementBannerText);
    }

    [Fact]
    public void SuspensionWhileRunningShowsTheEnforcementBanner()
    {
        var enforcementStateService = new Enforcement.FakeInstallationEnforcementStateService();
        var viewModel = CreateViewModel(enforcementStateService: enforcementStateService);

        enforcementStateService.RaiseStateChanged(Pos.Application.Enforcement.InstallationEnforcementState.Suspended);

        Assert.True(viewModel.HasEnforcementBanner);
        Assert.Contains("suspendida", viewModel.EnforcementBannerText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RecoveringFromSuspensionWhileRunningClearsTheEnforcementBanner()
    {
        var enforcementStateService = new Enforcement.FakeInstallationEnforcementStateService();
        var viewModel = CreateViewModel(enforcementStateService: enforcementStateService);
        enforcementStateService.RaiseStateChanged(Pos.Application.Enforcement.InstallationEnforcementState.Suspended);

        enforcementStateService.RaiseStateChanged(Pos.Application.Enforcement.InstallationEnforcementState.Allowed);

        Assert.False(viewModel.HasEnforcementBanner);
        Assert.Null(viewModel.EnforcementBannerText);
    }

    [Fact]
    public void DisposingStopsReactingToFurtherEnforcementStateChanges()
    {
        var enforcementStateService = new Enforcement.FakeInstallationEnforcementStateService();
        var viewModel = CreateViewModel(enforcementStateService: enforcementStateService);

        viewModel.Dispose();
        enforcementStateService.RaiseStateChanged(Pos.Application.Enforcement.InstallationEnforcementState.Suspended);

        Assert.False(viewModel.HasEnforcementBanner);
    }

    [Fact]
    public void MissingSessionProducesEmptyDisplayNameAndRoleNameInsteadOfThrowing()
    {
        var viewModel = CreateViewModel();

        Assert.Equal(string.Empty, viewModel.DisplayName);
        Assert.Equal(string.Empty, viewModel.RoleName);
    }

    [Fact]
    public void ExposesRegisterDataFromTheCurrentRegisterSession()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
        var registerSession = new FakeCurrentRegisterSession { Current = CreateActiveRegisterSession() };
        var viewModel = CreateViewModel(session: session, registerSession: registerSession);

        Assert.True(viewModel.IsRegisterOpen);
        Assert.Equal("Caja 1", viewModel.RegisterName);
        Assert.Equal("Caja abierta", viewModel.RegisterStatusText);
        Assert.Contains("100", viewModel.RegisterOpeningAmountText);
        Assert.Contains("MXN", viewModel.RegisterOpeningAmountText);
    }

    [Fact]
    public void NoOpenRegisterSessionProducesEmptyRegisterDataInsteadOfThrowing()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
        var viewModel = CreateViewModel(session: session);

        Assert.False(viewModel.IsRegisterOpen);
        Assert.Equal(string.Empty, viewModel.RegisterName);
        Assert.Equal(string.Empty, viewModel.RegisterStatusText);
        Assert.Equal(string.Empty, viewModel.RegisterOpeningAmountText);
    }

    // ---------- Logout / cerrar caja ----------

    [Fact]
    public void LogoutCommandClearsTheSessionWhenNoRegisterIsOpen()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
        var viewModel = CreateViewModel(session: session);

        viewModel.LogoutCommand.Execute(null);

        Assert.Equal(1, session.ClearCallCount);
        Assert.False(session.IsAuthenticated);
    }

    [Fact]
    public void LogoutCommandRaisesLogoutRequestedWhenNoRegisterIsOpen()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
        var viewModel = CreateViewModel(session: session);

        var raised = false;
        viewModel.LogoutRequested += (_, _) => raised = true;

        viewModel.LogoutCommand.Execute(null);

        Assert.True(raised);
    }

    [Fact]
    public void LogoutCommandIsBlockedWhenARegisterSessionIsOpen()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
        var registerSession = new FakeCurrentRegisterSession { Current = CreateActiveRegisterSession() };
        var viewModel = CreateViewModel(session: session, registerSession: registerSession);

        var raised = false;
        viewModel.LogoutRequested += (_, _) => raised = true;

        viewModel.LogoutCommand.Execute(null);

        Assert.False(raised);
        Assert.Equal(0, session.ClearCallCount);
        Assert.True(session.IsAuthenticated);
        Assert.False(string.IsNullOrEmpty(viewModel.LogoutBlockedMessage));
    }

    [Fact]
    public void LoggingOutTwiceDoesNotThrow()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
        var viewModel = CreateViewModel(session: session);

        viewModel.LogoutCommand.Execute(null);
        viewModel.LogoutCommand.Execute(null);

        Assert.Equal(2, session.ClearCallCount);
    }

    [Fact]
    public void LogoutCommandDoesNotClearTheCart()
    {
        var currentSalesCart = new FakeCurrentSalesCart();
        var viewModel = CreateViewModel(currentSalesCart: currentSalesCart);

        viewModel.LogoutCommand.Execute(null);

        Assert.Equal(0, currentSalesCart.ClearCallCount);
    }

    [Fact]
    public void CloseRegisterCommandRaisesCloseRegisterRequested()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
        var registerSession = new FakeCurrentRegisterSession { Current = CreateActiveRegisterSession() };
        var viewModel = CreateViewModel(session: session, registerSession: registerSession);

        var raised = false;
        viewModel.CloseRegisterRequested += (_, _) => raised = true;

        viewModel.CloseRegisterCommand.Execute(null);

        Assert.True(raised);
        Assert.Equal(0, registerSession.ClearCallCount);
    }

    // Corrección de la tarea (sección 10/22): una caja abierta cuando el heartbeat confirma un
    // estado restrictivo no debe requerir reiniciar la app ni usar SQL/terminal. La guarda
    // relevante vive en RegisterSessionService.CloseAsync (Application), no aquí: este ViewModel
    // nunca gateó CloseRegisterCommand por enforcement, así que el evento debe seguir
    // propagándose sin cambios mientras la instalación está restringida.
    [Theory]
    [InlineData(InstallationEnforcementState.Suspended)]
    [InlineData(InstallationEnforcementState.CredentialInvalid)]
    [InlineData(InstallationEnforcementState.Decommissioned)]
    public void CloseRegisterCommandRemainsAvailableWhileInstallationIsRestricted(
        InstallationEnforcementState restrictedState)
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
        var registerSession = new FakeCurrentRegisterSession { Current = CreateActiveRegisterSession() };
        var enforcementStateService = new Enforcement.FakeInstallationEnforcementStateService();
        var viewModel = CreateViewModel(
            session: session, registerSession: registerSession, enforcementStateService: enforcementStateService);

        enforcementStateService.RaiseStateChanged(restrictedState);

        var raised = false;
        viewModel.CloseRegisterRequested += (_, _) => raised = true;

        viewModel.CloseRegisterCommand.Execute(null);

        Assert.True(raised);
    }

    [Fact]
    public void CloseRegisterCommandIsBlockedWhenTheCartHasLines()
    {
        var currentSalesCart = new FakeCurrentSalesCart();
        currentSalesCart.SetSnapshot(new SalesCartSnapshot(
            [new SalesCartLine(ProductId.New(), "SKU-001", "Agua 1L", 1m, 10m, 10m, "MXN", 5m, true)], "MXN"));
        var viewModel = CreateViewModel(currentSalesCart: currentSalesCart);

        var raised = false;
        viewModel.CloseRegisterRequested += (_, _) => raised = true;

        viewModel.CloseRegisterCommand.Execute(null);

        Assert.False(raised);
        Assert.False(string.IsNullOrEmpty(viewModel.CloseRegisterBlockedMessage));
    }

    [Fact]
    public void RegisterViewCloseRegisterRequestedIsForwardedThroughTheSameBlockingRule()
    {
        var currentSalesCart = new FakeCurrentSalesCart();
        currentSalesCart.SetSnapshot(new SalesCartSnapshot(
            [new SalesCartLine(ProductId.New(), "SKU-001", "Agua 1L", 1m, 10m, 10m, "MXN", 5m, true)], "MXN"));
        var registerViewModel = new RegisterViewModel(
            new FakeCurrentRegisterSession(), new FakeCurrentUserSession(),
            new Pos.Desktop.Tests.Register.FakeCashMovementService(), NullLogger<RegisterViewModel>.Instance);
        var viewModel = CreateViewModel(currentSalesCart: currentSalesCart, registerViewModel: registerViewModel);

        var raised = false;
        viewModel.CloseRegisterRequested += (_, _) => raised = true;

        registerViewModel.CloseRegisterCommand.Execute(null);

        Assert.False(raised);
        Assert.False(string.IsNullOrEmpty(viewModel.CloseRegisterBlockedMessage));
    }

    [Fact]
    public void ConstructorDoesNotAcceptAServiceProviderOrResolveWindows()
    {
        var constructorParameterTypes = typeof(MainWindowViewModel)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(p => p.ParameterType.Name);

        Assert.DoesNotContain("IServiceProvider", constructorParameterTypes);
    }

    // ---------- Navegación (TAREA 24C / TAREA 24C.1) ----------

    [Fact]
    public void DashboardIsSelectedInitiallyForAnyAuthenticatedUser()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
        var dashboardViewModel = new DashboardViewModel(
            session, new FakeCurrentRegisterSession(), new FakeCurrentSalesCart(), new FakeProductManagementService());
        var viewModel = CreateViewModel(session: session, dashboardViewModel: dashboardViewModel);

        Assert.Equal(NavigationSection.Dashboard, viewModel.SelectedNavigationItem?.Section);
        Assert.Same(dashboardViewModel, viewModel.CurrentViewModel);
    }

    [Fact]
    public void DashboardIsVisibleEvenWithoutManageProductsOrRegisterPermissions()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
        var viewModel = CreateViewModel(session: session);

        Assert.Contains(viewModel.NavigationItems, i => i.Section == NavigationSection.Dashboard);
    }

    [Fact]
    public void NavigationItemsOnlyIncludeSectionsThePermissionsAllow()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
        var viewModel = CreateViewModel(session: session);

        Assert.Contains(viewModel.NavigationItems, i => i.Section == NavigationSection.Sales);
        Assert.DoesNotContain(viewModel.NavigationItems, i => i.Section == NavigationSection.Products);
        Assert.DoesNotContain(viewModel.NavigationItems, i => i.Section == NavigationSection.Settings);
    }

    // BASIC-CFG-01, sección 27/28: Configuración se gatea con ManageSettings, nunca con RoleName.
    [Fact]
    public void NavigationItemsIncludeLocalConfigurationForAUserWithManageSettings()
    {
        var session = new FakeCurrentUserSession
        {
            CurrentUser = new AuthenticatedUser(
                UserId.New(), OrganizationId.New(), RoleId.New(), "ADMIN", "Admin Uno", "Administrador",
                [Permission.ProcessSale, Permission.ManageSettings]),
        };
        var viewModel = CreateViewModel(session: session);

        Assert.Contains(viewModel.NavigationItems, i => i.Section == NavigationSection.LocalConfiguration);
    }

    [Fact]
    public void NavigationItemsExcludeLocalConfigurationForAUserWithoutManageSettings()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateManageProductsUser() };
        var viewModel = CreateViewModel(session: session);

        Assert.DoesNotContain(viewModel.NavigationItems, i => i.Section == NavigationSection.LocalConfiguration);
    }

    [Fact]
    public void SelectingLocalConfigurationNavigatesToTheLocalConfigurationViewModel()
    {
        var session = new FakeCurrentUserSession
        {
            CurrentUser = new AuthenticatedUser(
                UserId.New(), OrganizationId.New(), RoleId.New(), "ADMIN", "Admin Uno", "Administrador",
                [Permission.ManageSettings]),
        };
        var viewModel = CreateViewModel(session: session);

        viewModel.SelectedNavigationItem = viewModel.NavigationItems.Single(i => i.Section == NavigationSection.LocalConfiguration);

        Assert.IsType<Pos.Desktop.LocalConfiguration.LocalConfigurationViewModel>(viewModel.CurrentViewModel);
    }

    [Fact]
    public void SelectingDashboardAfterAnotherSectionRestoresTheDashboardViewModel()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateManageProductsUser() };
        var viewModel = CreateViewModel(session: session);

        viewModel.SelectedNavigationItem = viewModel.NavigationItems.Single(i => i.Section == NavigationSection.Products);
        viewModel.SelectedNavigationItem = viewModel.NavigationItems.Single(i => i.Section == NavigationSection.Dashboard);

        Assert.IsType<DashboardViewModel>(viewModel.CurrentViewModel);
    }

    // ---------- Sidebar colapsable (TAREA 24C.1, sección 4) ----------

    [Fact]
    public void SidebarStartsExpanded()
    {
        var viewModel = CreateViewModel();

        Assert.True(viewModel.IsSidebarExpanded);
    }

    [Fact]
    public void ToggleSidebarCommandCollapsesThenExpandsAgain()
    {
        var viewModel = CreateViewModel();
        var expandedWidth = viewModel.SidebarWidth;

        viewModel.ToggleSidebarCommand.Execute(null);
        Assert.False(viewModel.IsSidebarExpanded);
        var collapsedWidth = viewModel.SidebarWidth;

        viewModel.ToggleSidebarCommand.Execute(null);
        Assert.True(viewModel.IsSidebarExpanded);

        Assert.NotEqual(expandedWidth, collapsedWidth);
        Assert.Equal(expandedWidth, viewModel.SidebarWidth);
    }

    [Fact]
    public void TogglingTheSidebarDoesNotChangeTheSelectedNavigationItem()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateManageProductsUser() };
        var viewModel = CreateViewModel(session: session);
        viewModel.SelectedNavigationItem = viewModel.NavigationItems.Single(i => i.Section == NavigationSection.Products);

        viewModel.ToggleSidebarCommand.Execute(null);

        Assert.Equal(NavigationSection.Products, viewModel.SelectedNavigationItem?.Section);
    }

    [Fact]
    public void NavigationItemsIncludeProductsAndInventoryForAManageProductsUser()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateManageProductsUser() };
        var viewModel = CreateViewModel(session: session);

        Assert.Contains(viewModel.NavigationItems, i => i.Section == NavigationSection.Products);
        Assert.Contains(viewModel.NavigationItems, i => i.Section == NavigationSection.Inventory);
    }

    // READ-ONLY CORRECTION (sección 12 de la tarea): el módulo Inventario ahora se gatea
    // exclusivamente con ViewInventory (lectura), separado de AdjustInventory (mutación) -
    // "Queries → ViewInventory. Mutations → AdjustInventory". AdjustInventory sin ViewInventory ya
    // no basta para ver el módulo.
    [Fact]
    public void NavigationItemsIncludeInventoryForAUserWithViewInventoryPermission()
    {
        var session = new FakeCurrentUserSession
        {
            CurrentUser = new AuthenticatedUser(
                UserId.New(), OrganizationId.New(), RoleId.New(), "USERNAME", "Ana Pérez", "Almacenista",
                [Permission.ProcessSale, Permission.ViewInventory]),
        };
        var viewModel = CreateViewModel(session: session);

        Assert.Contains(viewModel.NavigationItems, i => i.Section == NavigationSection.Inventory);
    }

    [Fact]
    public void NavigationItemsExcludeInventoryForAUserWithOnlyAdjustInventoryPermission()
    {
        var session = new FakeCurrentUserSession
        {
            CurrentUser = new AuthenticatedUser(
                UserId.New(), OrganizationId.New(), RoleId.New(), "USERNAME", "Ana Pérez", "Almacenista",
                [Permission.ProcessSale, Permission.AdjustInventory]),
        };
        var viewModel = CreateViewModel(session: session);

        Assert.DoesNotContain(viewModel.NavigationItems, i => i.Section == NavigationSection.Inventory);
    }

    [Fact]
    public void NavigationItemsExcludeInventoryForAUserWithNeitherPermission()
    {
        var session = new FakeCurrentUserSession
        {
            CurrentUser = new AuthenticatedUser(
                UserId.New(), OrganizationId.New(), RoleId.New(), "USERNAME", "Ana Pérez", "Cajero",
                [Permission.ProcessSale]),
        };
        var viewModel = CreateViewModel(session: session);

        Assert.DoesNotContain(viewModel.NavigationItems, i => i.Section == NavigationSection.Inventory);
    }

    [Fact]
    public void NavigationItemsIncludeRegisterForAUserWithOpenOrCloseRegisterPermission()
    {
        var session = new FakeCurrentUserSession
        {
            CurrentUser = new AuthenticatedUser(
                UserId.New(), OrganizationId.New(), RoleId.New(), "USERNAME", "Ana Pérez", "Cajero",
                [Permission.ProcessSale, Permission.CloseRegisterSession]),
        };
        var viewModel = CreateViewModel(session: session);

        Assert.Contains(viewModel.NavigationItems, i => i.Section == NavigationSection.Register);
    }

    // ---------- Auditoría (TAREA 24D, sección 16/23) ----------

    [Fact]
    public void NavigationItemsIncludeAuditWithProductsChildForAUserWithViewProductAudit()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateViewProductAuditUser() };
        var viewModel = CreateViewModel(session: session);

        var auditItem = Assert.Single(viewModel.NavigationItems, i => i.Section == NavigationSection.Audit);
        Assert.True(auditItem.HasChildren);
        Assert.Contains(auditItem.Children!, child => child.Section == NavigationSection.AuditProducts);
    }

    [Fact]
    public void NavigationItemsExcludeAuditWithoutViewProductAuditPermission()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateManageProductsUser() };
        var viewModel = CreateViewModel(session: session);

        Assert.DoesNotContain(viewModel.NavigationItems, i => i.Section == NavigationSection.Audit);
    }

    [Fact]
    public void SelectingTheAuditParentItemExpandsItsChildWithoutChangingCurrentViewModel()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateViewProductAuditUser() };
        var viewModel = CreateViewModel(session: session);
        var previousViewModel = viewModel.CurrentViewModel;
        var auditItem = viewModel.NavigationItems.Single(i => i.Section == NavigationSection.Audit);

        viewModel.SelectedNavigationItem = auditItem;

        Assert.Same(previousViewModel, viewModel.CurrentViewModel);
        Assert.Contains(viewModel.NavigationItems, i => i.Section == NavigationSection.AuditProducts);
    }

    [Fact]
    public void SelectingTheAuditProductsChildNavigatesToTheProductAuditViewModel()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateViewProductAuditUser() };
        var viewModel = CreateViewModel(session: session);
        var auditItem = viewModel.NavigationItems.Single(i => i.Section == NavigationSection.Audit);
        viewModel.SelectedNavigationItem = auditItem;
        var auditProductsItem = viewModel.NavigationItems.Single(i => i.Section == NavigationSection.AuditProducts);

        viewModel.SelectedNavigationItem = auditProductsItem;

        Assert.IsType<ProductAuditViewModel>(viewModel.CurrentViewModel);
    }

    [Fact]
    public void ViewAuditDetailFromProductsAppliesTheProductFilterAndNavigatesToAuditProducts()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateViewProductAuditUser() };
        var productsViewModel = new ProductsViewModel(new CatalogFakeProductManagementService(), session);
        var viewModel = CreateViewModel(session: session, productsViewModel: productsViewModel);
        var item = new ProductCatalogItem(
            ProductId.New(), "SKU-001", null, "Agua 1L", 10m, "MXN", true, 5m, 2m, true);

        productsViewModel.ViewAuditDetailCommand.Execute(item);

        var auditViewModel = Assert.IsType<ProductAuditViewModel>(viewModel.CurrentViewModel);
        Assert.True(auditViewModel.HasProductFilter);
        Assert.Equal("Producto: SKU-001", auditViewModel.ProductFilterLabel);
        Assert.Contains(viewModel.NavigationItems, i => i.Section == NavigationSection.AuditProducts);
    }

    // ---------- Ventas > Punto de venta / Historial (TAREA 25B, sección 9/49) ----------

    [Fact]
    public void UserWithOnlyProcessSaleSeesPointOfSaleButNotHistory()
    {
        var session = new FakeCurrentUserSession
        {
            CurrentUser = new AuthenticatedUser(
                UserId.New(), OrganizationId.New(), RoleId.New(), "CAJERO", "Ana Pérez", "Cajero",
                [Permission.ProcessSale]),
        };
        var viewModel = CreateViewModel(session: session);

        var salesItem = viewModel.NavigationItems.Single(i => i.Section == NavigationSection.Sales);
        Assert.Contains(salesItem.Children!, child => child.Section == NavigationSection.SalesPointOfSale);
        Assert.DoesNotContain(salesItem.Children!, child => child.Section == NavigationSection.SalesHistory);
    }

    // READ-ONLY CORRECTION (sección 14 de la tarea): Historial se gatea con ViewSalesHistory, no
    // con ViewReports - "Do NOT equate Sales History with Administrative Reports".
    [Fact]
    public void UserWithOnlyViewSalesHistorySeesHistoryButNotPointOfSale()
    {
        var session = new FakeCurrentUserSession
        {
            CurrentUser = new AuthenticatedUser(
                UserId.New(), OrganizationId.New(), RoleId.New(), "CAJERO", "Ana Pérez", "Cajero",
                [Permission.ViewSalesHistory]),
        };
        var viewModel = CreateViewModel(session: session);

        var salesItem = viewModel.NavigationItems.Single(i => i.Section == NavigationSection.Sales);
        Assert.Contains(salesItem.Children!, child => child.Section == NavigationSection.SalesHistory);
        Assert.DoesNotContain(salesItem.Children!, child => child.Section == NavigationSection.SalesPointOfSale);
    }

    [Fact]
    public void UserWithOnlyViewReportsDoesNotSeeHistory()
    {
        var session = new FakeCurrentUserSession
        {
            CurrentUser = new AuthenticatedUser(
                UserId.New(), OrganizationId.New(), RoleId.New(), "GERENTE", "Ana Pérez", "Gerente",
                [Permission.ViewReports]),
        };
        var viewModel = CreateViewModel(session: session);

        Assert.DoesNotContain(viewModel.NavigationItems, i => i.Section == NavigationSection.Sales);
    }

    [Fact]
    public void UserWithBothPermissionsSeesBothSalesChildren()
    {
        var session = new FakeCurrentUserSession
        {
            CurrentUser = new AuthenticatedUser(
                UserId.New(), OrganizationId.New(), RoleId.New(), "GERENTE", "Ana Pérez", "Gerente",
                [Permission.ProcessSale, Permission.ViewSalesHistory]),
        };
        var viewModel = CreateViewModel(session: session);

        var salesItem = viewModel.NavigationItems.Single(i => i.Section == NavigationSection.Sales);
        Assert.Contains(salesItem.Children!, child => child.Section == NavigationSection.SalesPointOfSale);
        Assert.Contains(salesItem.Children!, child => child.Section == NavigationSection.SalesHistory);
    }

    [Fact]
    public void UserWithNeitherProcessSaleNorViewReportsDoesNotSeeTheSalesParent()
    {
        var session = new FakeCurrentUserSession
        {
            CurrentUser = new AuthenticatedUser(
                UserId.New(), OrganizationId.New(), RoleId.New(), "ALMACEN", "Ana Pérez", "Almacenista",
                [Permission.AdjustInventory]),
        };
        var viewModel = CreateViewModel(session: session);

        Assert.DoesNotContain(viewModel.NavigationItems, i => i.Section == NavigationSection.Sales);
    }

    [Fact]
    public void SelectingTheSalesParentItemExpandsItsChildrenWithoutChangingCurrentViewModel()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateViewReportsUser() };
        var viewModel = CreateViewModel(session: session);
        var previousViewModel = viewModel.CurrentViewModel;
        var salesItem = viewModel.NavigationItems.Single(i => i.Section == NavigationSection.Sales);

        viewModel.SelectedNavigationItem = salesItem;

        Assert.Same(previousViewModel, viewModel.CurrentViewModel);
        Assert.Contains(viewModel.NavigationItems, i => i.Section == NavigationSection.SalesHistory);
    }

    [Fact]
    public void SelectingTheSalesHistoryChildNavigatesToTheSalesHistoryViewModel()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateViewReportsUser() };
        var viewModel = CreateViewModel(session: session);
        var salesItem = viewModel.NavigationItems.Single(i => i.Section == NavigationSection.Sales);
        viewModel.SelectedNavigationItem = salesItem;
        var historyItem = viewModel.NavigationItems.Single(i => i.Section == NavigationSection.SalesHistory);

        viewModel.SelectedNavigationItem = historyItem;

        Assert.IsType<SalesHistoryViewModel>(viewModel.CurrentViewModel);
    }

    // TAREA 25B-FIX, sección 9/17: navegar a Ventas > Historial por primera vez debe disparar,
    // automáticamente y sin presionar "Buscar", las tres llamadas de la carga inicial (página 1,
    // resumen, opciones de filtro), y nunca debe dejar el ViewModel mostrando Detail.
    [Fact]
    public void NavigatingToSalesHistoryTriggersItsLoadCommand()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateViewReportsUser() };
        var salesHistoryService = new FakeSalesHistoryService();
        var salesHistoryViewModel = new SalesHistoryViewModel(salesHistoryService, new FakeCurrentUserSession(), new FakeReceiptPrintingService(), new FakeClock(DateTimeOffset.UtcNow));
        var viewModel = CreateViewModel(session: session, salesHistoryViewModel: salesHistoryViewModel);
        var salesItem = viewModel.NavigationItems.Single(i => i.Section == NavigationSection.Sales);
        viewModel.SelectedNavigationItem = salesItem;
        var historyItem = viewModel.NavigationItems.Single(i => i.Section == NavigationSection.SalesHistory);

        viewModel.SelectedNavigationItem = historyItem;

        Assert.Equal(1, salesHistoryService.SearchPageCallCount);
        Assert.Equal(0, salesHistoryService.LastSkip);
        Assert.Equal(1, salesHistoryService.GetSummaryCallCount);
        Assert.Equal(1, salesHistoryService.GetFilterOptionsCallCount);
        Assert.False(salesHistoryViewModel.IsShowingDetail);
        Assert.True(salesHistoryViewModel.IsShowingList);
    }

    // ---------- Reportes (BASIC-RPT-01, sección 6/7) ----------

    [Fact]
    public void NavigationItemsIncludeReportsForAUserWithViewReportsPermission()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateViewReportsUser() };
        var viewModel = CreateViewModel(session: session);

        Assert.Contains(viewModel.NavigationItems, i => i.Section == NavigationSection.Reports);
    }

    // Cashier (ProcessSale + ViewSalesHistory, sin ViewReports) no debe ver Reportes: matriz
    // congelada de la tarea, sección 6/29.
    [Fact]
    public void NavigationItemsExcludeReportsForACashierWithoutViewReportsPermission()
    {
        var session = new FakeCurrentUserSession
        {
            CurrentUser = new AuthenticatedUser(
                UserId.New(), OrganizationId.New(), RoleId.New(), "CAJERO", "Ana Pérez", "Cajero",
                [Permission.ProcessSale, Permission.ViewSalesHistory]),
        };
        var viewModel = CreateViewModel(session: session);

        Assert.DoesNotContain(viewModel.NavigationItems, i => i.Section == NavigationSection.Reports);
    }

    [Fact]
    public void SelectingReportsNavigatesToTheReportsViewModel()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateViewReportsUser() };
        var viewModel = CreateViewModel(session: session);

        viewModel.SelectedNavigationItem = viewModel.NavigationItems.Single(i => i.Section == NavigationSection.Reports);

        Assert.IsType<Pos.Desktop.Reports.ReportsViewModel>(viewModel.CurrentViewModel);
    }

    [Fact]
    public void NavigatingToReportsTriggersItsLoadCommand()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateViewReportsUser() };
        var reportsService = new Pos.Desktop.Tests.Reports.FakeOperationalReportsService();
        var reportsViewModel = new Pos.Desktop.Reports.ReportsViewModel(reportsService, new FakeClock(DateTimeOffset.UtcNow));
        var viewModel = CreateViewModel(session: session, reportsViewModel: reportsViewModel);

        viewModel.SelectedNavigationItem = viewModel.NavigationItems.Single(i => i.Section == NavigationSection.Reports);

        Assert.Equal(1, reportsService.GetSalesSummaryCallCount);
        Assert.Equal(1, reportsService.GetRegisterClosuresCallCount);
        Assert.Equal(1, reportsService.GetCashMovementsCallCount);
        Assert.Equal(1, reportsService.GetProductSalesCallCount);
        Assert.Equal(1, reportsService.GetLowStockCallCount);
        Assert.Equal(1, reportsService.GetOperatorActivityCallCount);
    }

    // ---------- Centro de notificaciones (TAREA 24E, sección 29/30) ----------

    [Fact]
    public void CanViewNotificationsIsTrueWithViewProductAuditPermission()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateViewProductAuditUser() };
        var viewModel = CreateViewModel(session: session);

        Assert.True(viewModel.CanViewNotifications);
    }

    [Fact]
    public void CanViewNotificationsIsFalseWithoutViewProductAuditPermission()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateManageProductsUser() };
        var viewModel = CreateViewModel(session: session);

        Assert.False(viewModel.CanViewNotifications);
    }

    // Click en una notificación: navega a Auditoría > Productos y selecciona exactamente el
    // AuditEvent referenciado, nunca "el más reciente del producto" (TAREA 24E, sección 30/47).
    [Fact]
    public async Task OpeningANotificationNavigatesToAuditProductsWithTheExactAuditEventFilter()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateViewProductAuditUser() };
        var productId = ProductId.New();
        var auditEventId = ProductAuditEventId.New();
        var item = new AdministrativeNotificationItem(
            AdministrativeNotificationId.New(), auditEventId, productId, "SKU-001", "Agua 1L", "Administrador",
            ProductAuditAction.Updated, new DateTimeOffset(2026, 8, 2, 14, 22, 0, TimeSpan.Zero), null,
            [new ProductAuditFieldChange(ProductAuditField.SalePrice, "MXN 25.00", "MXN 27.50")]);
        var notificationService = new FakeAdministrativeNotificationService
        {
            PageResult = new AdministrativeNotificationPageResult([item], false),
        };
        var productAuditService = new FakeProductAuditService();
        var viewModel = CreateViewModel(session: session, notificationService: notificationService, productAuditService: productAuditService);

        viewModel.NotificationCenterViewModel.ToggleCommand.Execute(null);
        await Task.Yield();
        var row = Assert.Single(viewModel.NotificationCenterViewModel.Notifications);

        viewModel.NotificationCenterViewModel.OpenNotificationCommand.Execute(row);
        await Task.Yield();

        var auditViewModel = Assert.IsType<ProductAuditViewModel>(viewModel.CurrentViewModel);
        Assert.True(auditViewModel.HasProductFilter);
        Assert.Equal("Producto: SKU-001", auditViewModel.ProductFilterLabel);
        Assert.Contains(viewModel.NavigationItems, i => i.Section == NavigationSection.AuditProducts);

        // ApplySection dispara LoadCommand al navegar: el filtro ya viajó hasta el query real.
        Assert.Equal(auditEventId, productAuditService.LastFilter!.AuditEventId);
        Assert.Equal(productId, productAuditService.LastFilter.ProductId);
    }

    // ---------- Refresh del badge tras operaciones locales de Producto (TAREA 24E, sección 32/33) ----------

    [Fact]
    public void ApplyProductCreatedRefreshesTheUnreadCount()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateManageProductsUser() };
        var notificationService = new FakeAdministrativeNotificationService();
        var viewModel = CreateViewModel(session: session, notificationService: notificationService);
        var callsAfterConstruction = notificationService.GetUnreadCountCallCount;

        viewModel.ApplyProductCreated("SKU-NEW");

        Assert.True(notificationService.GetUnreadCountCallCount > callsAfterConstruction);
    }

    [Fact]
    public void ApplyProductUpdatedRefreshesTheUnreadCount()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateManageProductsUser() };
        var notificationService = new FakeAdministrativeNotificationService();
        var viewModel = CreateViewModel(session: session, notificationService: notificationService);
        var callsAfterConstruction = notificationService.GetUnreadCountCallCount;

        viewModel.ApplyProductUpdated("SKU-EDITED");

        Assert.True(notificationService.GetUnreadCountCallCount > callsAfterConstruction);
    }

    // TAREA 24G, sección 19/50: un ajuste exitoso desde Inventario refresca el badge de
    // notificaciones exactamente igual que un ajuste hecho desde Productos (misma política, sin
    // duplicarla en Desktop).
    [Fact]
    public void ApplyInventoryAdjustedRefreshesTheUnreadCount()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateManageProductsUser() };
        var notificationService = new FakeAdministrativeNotificationService();
        var viewModel = CreateViewModel(session: session, notificationService: notificationService);
        var callsAfterConstruction = notificationService.GetUnreadCountCallCount;

        viewModel.ApplyInventoryAdjusted();

        Assert.True(notificationService.GetUnreadCountCallCount > callsAfterConstruction);
    }

    // TAREA 24G, sección 16/17: AdjustInventoryWindow se abre directamente desde Inventario (sin
    // pasar por EditProductWindow), igual patrón de burbujeo que EditProductRequested.
    [Fact]
    public void AdjustInventoryRequestedFromInventoryBubblesUpWithTheItem()
    {
        var session = new FakeCurrentUserSession
        {
            CurrentUser = new AuthenticatedUser(
                UserId.New(), OrganizationId.New(), RoleId.New(), "GERENTE", "Ana Pérez", "Gerente",
                [Permission.ProcessSale, Permission.ManageProducts, Permission.AdjustInventory]),
        };
        var inventoryItem = new Pos.Application.Inventory.InventoryCatalogItem(
            ProductId.New(), "SKU-001", null, "Producto", true, 5m, 2m,
            Pos.Application.Inventory.InventoryStockStatus.InStock);
        var inventoryViewModel = new InventoryViewModel(
            new FakeInventoryService(catalogHandler: (_, _, _, _, _) =>
                Task.FromResult(new Pos.Application.Inventory.InventoryCatalogPageResult([inventoryItem], false))),
            session);
        var viewModel = CreateViewModel(session: session, inventoryViewModel: inventoryViewModel);
        inventoryViewModel.LoadCommand.Execute(null);

        Pos.Application.Inventory.InventoryCatalogItem? raised = null;
        viewModel.AdjustInventoryRequested += (_, item) => raised = item;

        inventoryViewModel.AdjustCommand.Execute(inventoryViewModel.Items.Single());

        Assert.Equal(inventoryItem.ProductId, raised?.ProductId);
    }

    [Fact]
    public void ConstructingTheShellLoadsTheInitialUnreadCountWithoutPolling()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateViewProductAuditUser() };
        var notificationService = new FakeAdministrativeNotificationService { UnreadCount = 2 };

        var viewModel = CreateViewModel(session: session, notificationService: notificationService);

        Assert.Equal(2, viewModel.NotificationCenterViewModel.UnreadCount);
        Assert.Equal(1, notificationService.GetUnreadCountCallCount);
    }

    [Fact]
    public void SelectingProductsChangesCurrentViewModelToTheProductsViewModel()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateManageProductsUser() };
        var productsViewModel = new ProductsViewModel(new FakeProductManagementService(), session);
        var viewModel = CreateViewModel(session: session, productsViewModel: productsViewModel);

        viewModel.SelectedNavigationItem = viewModel.NavigationItems.Single(i => i.Section == NavigationSection.Products);

        Assert.Same(productsViewModel, viewModel.CurrentViewModel);
    }

    [Fact]
    public void NavigatingToSalesAndBackToProductsKeepsTheSameChildViewModelInstances()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateManageProductsUser() };
        var salesViewModel = new SalesViewModel(session, new FakeCurrentRegisterSession(), new FakeSalesCartService(), new FakeProductManagementService(), new FakeCurrentSalesCart());
        var productsViewModel = new ProductsViewModel(new FakeProductManagementService(), session);
        var viewModel = CreateViewModel(session: session, salesViewModel: salesViewModel, productsViewModel: productsViewModel);

        viewModel.SelectedNavigationItem = viewModel.NavigationItems.Single(i => i.Section == NavigationSection.Products);
        viewModel.SelectedNavigationItem = viewModel.NavigationItems.Single(i => i.Section == NavigationSection.Sales);
        viewModel.SelectedNavigationItem = viewModel.NavigationItems.Single(i => i.Section == NavigationSection.SalesPointOfSale);

        Assert.Same(salesViewModel, viewModel.CurrentViewModel);
    }

    [Fact]
    public void NavigatingToSalesDoesNotClearTheCart()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateManageProductsUser() };
        var currentSalesCart = new FakeCurrentSalesCart();
        currentSalesCart.SetSnapshot(new SalesCartSnapshot(
            [new SalesCartLine(ProductId.New(), "SKU-001", "Agua 1L", 1m, 10m, 10m, "MXN", 5m, true)], "MXN"));
        var salesViewModel = new SalesViewModel(session, new FakeCurrentRegisterSession(), new FakeSalesCartService(), new FakeProductManagementService(), currentSalesCart);
        var productsViewModel = new ProductsViewModel(new FakeProductManagementService(), session);
        var viewModel = CreateViewModel(
            session: session, currentSalesCart: currentSalesCart, salesViewModel: salesViewModel, productsViewModel: productsViewModel);

        viewModel.SelectedNavigationItem = viewModel.NavigationItems.Single(i => i.Section == NavigationSection.Products);
        viewModel.SelectedNavigationItem = viewModel.NavigationItems.Single(i => i.Section == NavigationSection.Sales);
        viewModel.SelectedNavigationItem = viewModel.NavigationItems.Single(i => i.Section == NavigationSection.SalesPointOfSale);

        Assert.Single(salesViewModel.CartLines);
    }

    // ---------- Nuevo/editar producto reenviados desde las páginas hijas ----------

    [Fact]
    public void SalesNewProductRequestedBubblesUpToTheShellEvent()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
        var salesViewModel = new SalesViewModel(session, new FakeCurrentRegisterSession(), new FakeSalesCartService(), new FakeProductManagementService(), new FakeCurrentSalesCart());
        var viewModel = CreateViewModel(session: session, salesViewModel: salesViewModel);

        var raised = false;
        viewModel.NewProductRequested += (_, _) => raised = true;

        salesViewModel.NewProductCommand.Execute(null);

        Assert.True(raised);
    }

    [Fact]
    public void ApplyProductCreatedRoutesToTheSalesViewModelWhenItOriginatedTheRequest()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
        var salesCartService = new FakeSalesCartService();
        var salesViewModel = new SalesViewModel(session, new FakeCurrentRegisterSession(), salesCartService, new FakeProductManagementService(), new FakeCurrentSalesCart());
        var viewModel = CreateViewModel(session: session, salesViewModel: salesViewModel);

        salesViewModel.NewProductCommand.Execute(null);
        viewModel.ApplyProductCreated("SKU-NEW");

        Assert.Equal("SKU-NEW", salesViewModel.SearchText);
        Assert.Equal(1, salesCartService.SearchCallCount);
    }

    // ---------- Cobrar reenviado desde SalesViewModel (TAREA 25A) ----------

    [Fact]
    public void SalesCheckoutRequestedBubblesUpToTheShellEvent()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
        var registerSession = new FakeCurrentRegisterSession { Current = CreateActiveRegisterSession() };
        var currentSalesCart = new FakeCurrentSalesCart();
        currentSalesCart.SetSnapshot(new SalesCartSnapshot(
            [new SalesCartLine(ProductId.New(), "SKU-001", "Agua 1L", 1m, 10m, 10m, "MXN", 5m, true)], "MXN"));
        var salesViewModel = new SalesViewModel(
            session, registerSession, new FakeSalesCartService(), new FakeProductManagementService(), currentSalesCart);
        var viewModel = CreateViewModel(session: session, salesViewModel: salesViewModel);

        var raised = false;
        viewModel.CheckoutRequested += (_, _) => raised = true;

        salesViewModel.CheckoutCommand.Execute(null);

        Assert.True(raised);
    }

    [Fact]
    public void ApplyCheckoutCompletedRoutesToTheSalesViewModel()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
        var currentSalesCart = new FakeCurrentSalesCart();
        currentSalesCart.SetSnapshot(new SalesCartSnapshot(
            [new SalesCartLine(ProductId.New(), "SKU-001", "Agua 1L", 1m, 10m, 10m, "MXN", 5m, true)], "MXN"));
        var salesViewModel = new SalesViewModel(
            session, new FakeCurrentRegisterSession(), new FakeSalesCartService(), new FakeProductManagementService(), currentSalesCart);
        var viewModel = CreateViewModel(session: session, salesViewModel: salesViewModel);
        Assert.Single(salesViewModel.CartLines);

        currentSalesCart.Clear();
        viewModel.ApplyCheckoutCompleted();

        Assert.Empty(salesViewModel.CartLines);
    }

    [Fact]
    public void ApplyProductCreatedRoutesToTheProductsViewModelWhenItOriginatedTheRequest()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateManageProductsUser() };
        var productManagementService = new CatalogFakeProductManagementService();
        var productsViewModel = new ProductsViewModel(productManagementService, session);
        var viewModel = CreateViewModel(session: session, productsViewModel: productsViewModel);

        productsViewModel.NewProductCommand.Execute(null);
        viewModel.ApplyProductCreated("SKU-NEW");

        Assert.Equal("SKU-NEW", productManagementService.LastSearchTerm);
    }

    [Fact]
    public void EditProductRequestedFromProductsBubblesUpWithTheProductId()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateManageProductsUser() };
        var productsViewModel = new ProductsViewModel(new CatalogFakeProductManagementService(), session);
        var viewModel = CreateViewModel(session: session, productsViewModel: productsViewModel);
        var item = new ProductCatalogItem(
            ProductId.New(), "SKU-001", null, "Producto", 10m, "MXN", true, 5m, 2m, true);

        ProductId? raisedProductId = null;
        viewModel.EditProductRequested += (_, productId) => raisedProductId = productId;

        productsViewModel.EditProductCommand.Execute(item);

        Assert.Equal(item.ProductId, raisedProductId);
    }

    private static MainWindowViewModel CreateViewModel(
        FakeCurrentUserSession? session = null,
        FakeCurrentRegisterSession? registerSession = null,
        FakeCurrentSalesCart? currentSalesCart = null,
        DashboardViewModel? dashboardViewModel = null,
        SalesViewModel? salesViewModel = null,
        SalesHistoryViewModel? salesHistoryViewModel = null,
        ProductsViewModel? productsViewModel = null,
        InventoryViewModel? inventoryViewModel = null,
        RegisterViewModel? registerViewModel = null,
        ReportsViewModel? reportsViewModel = null,
        LocalConfigurationViewModel? localConfigurationViewModel = null,
        FakeAdministrativeNotificationService? notificationService = null,
        FakeProductAuditService? productAuditService = null,
        Enforcement.FakeInstallationEnforcementStateService? enforcementStateService = null,
        Enforcement.FakeInstallationConnectivityStateService? connectivityStateService = null,
        Pos.Desktop.Tests.Common.FakeDesktopClock? clock = null)
    {
        session ??= new FakeCurrentUserSession();
        registerSession ??= new FakeCurrentRegisterSession();
        currentSalesCart ??= new FakeCurrentSalesCart();
        enforcementStateService ??= new Enforcement.FakeInstallationEnforcementStateService();
        connectivityStateService ??= new Enforcement.FakeInstallationConnectivityStateService();
        clock ??= new Pos.Desktop.Tests.Common.FakeDesktopClock(new DateTime(2026, 8, 22, 18, 45, 0));
        dashboardViewModel ??= new DashboardViewModel(session, registerSession, currentSalesCart, new CatalogFakeProductManagementService());
        salesViewModel ??= new SalesViewModel(session, registerSession, new FakeSalesCartService(), new FakeProductManagementService(), currentSalesCart);
        salesHistoryViewModel ??= new SalesHistoryViewModel(new FakeSalesHistoryService(), new FakeCurrentUserSession(), new FakeReceiptPrintingService(), new FakeClock(DateTimeOffset.UtcNow));
        productsViewModel ??= new ProductsViewModel(new CatalogFakeProductManagementService(), session);
        inventoryViewModel ??= new InventoryViewModel(new FakeInventoryService(), session);
        registerViewModel ??= new RegisterViewModel(
            registerSession, session, new Pos.Desktop.Tests.Register.FakeCashMovementService(),
            NullLogger<RegisterViewModel>.Instance);
        reportsViewModel ??= new ReportsViewModel(
            new Pos.Desktop.Tests.Reports.FakeOperationalReportsService(), new FakeClock(DateTimeOffset.UtcNow));
        localConfigurationViewModel ??= Pos.Desktop.Tests.LocalConfiguration.LocalConfigurationViewModelTestFactory.CreateDefault();
        var userManagementViewModel = new UserManagementViewModel(new FakeUserManagementService());
        var productAuditViewModel = new ProductAuditViewModel(productAuditService ?? new FakeProductAuditService());
        var notificationCenterViewModel = new NotificationCenterViewModel(notificationService ?? new FakeAdministrativeNotificationService());

        return new MainWindowViewModel(
            session, registerSession, currentSalesCart, enforcementStateService,
            connectivityStateService, clock,
            dashboardViewModel, salesViewModel, salesHistoryViewModel, productsViewModel,
            inventoryViewModel, registerViewModel, userManagementViewModel, productAuditViewModel,
            reportsViewModel, localConfigurationViewModel, notificationCenterViewModel);
    }

    private static AuthenticatedUser CreateAuthenticatedUser(string displayName, string roleName) =>
        new(
            UserId.New(),
            OrganizationId.New(),
            RoleId.New(),
            "USERNAME",
            displayName,
            roleName,
            [Permission.ProcessSale]);

    // READ-ONLY CORRECTION: incluye ViewProducts/ViewInventory además de ManageProducts/
    // AdjustInventory (Manager, igual que el StandardRoles canónico, recibe ambos - lectura y
    // administración), para que los muchos tests que reutilizan este helper para navegar a
    // Productos/Inventario sigan viendo esas secciones bajo el nuevo gate (ViewProducts/
    // ViewInventory en vez de ManageProducts/AdjustInventory - sección 9/12 de la tarea).
    private static AuthenticatedUser CreateManageProductsUser() =>
        new(
            UserId.New(),
            OrganizationId.New(),
            RoleId.New(),
            "GERENTE",
            "Ana Pérez",
            "Gerente",
            [Permission.ProcessSale, Permission.ManageProducts, Permission.AdjustInventory, Permission.ViewProducts, Permission.ViewInventory]);

    // READ-ONLY CORRECTION: incluye ViewSalesHistory además de ViewReports, para que los tests que
    // reutilizan este helper para navegar a Ventas > Historial sigan viendo esa sección bajo el
    // nuevo gate (ViewSalesHistory, decoupled de ViewReports - sección 14 de la tarea: Sales
    // History no es lo mismo que Administrative Reports).
    private static AuthenticatedUser CreateViewReportsUser() =>
        new(
            UserId.New(),
            OrganizationId.New(),
            RoleId.New(),
            "GERENTE",
            "Ana Pérez",
            "Gerente",
            [Permission.ViewReports, Permission.ViewSalesHistory]);

    private static AuthenticatedUser CreateViewProductAuditUser() =>
        new(
            UserId.New(),
            OrganizationId.New(),
            RoleId.New(),
            "ADMIN",
            "Admin Uno",
            "Administrador",
            [Permission.ProcessSale, Permission.ManageProducts, Permission.ViewProductAudit]);

    private static ActiveRegisterSession CreateActiveRegisterSession() =>
        new(
            RegisterSessionId.New(),
            OrganizationId.New(),
            BranchId.New(),
            RegisterId.New(),
            "Caja 1",
            UserId.New(),
            "Ana Pérez",
            OpenedAtUtc,
            100m,
            "MXN");
}
