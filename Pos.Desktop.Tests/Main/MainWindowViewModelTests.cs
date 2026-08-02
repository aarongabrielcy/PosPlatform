using System.Linq;
using Pos.Application.Authentication;
using Pos.Application.Products.ManageProduct;
using Pos.Application.RegisterSessions;
using Pos.Application.SalesCart;
using Pos.Desktop.Dashboard;
using Pos.Desktop.Inventory;
using Pos.Desktop.Main;
using Pos.Desktop.Products.Catalog;
using Pos.Desktop.Register;
using Pos.Desktop.Sales;
using Pos.Desktop.Settings;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;
using CatalogFakeProductManagementService = Pos.Desktop.Tests.Products.Catalog.FakeProductManagementService;

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
        var registerViewModel = new RegisterViewModel(new FakeCurrentRegisterSession());
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

    [Fact]
    public void SelectingProductsChangesCurrentViewModelToTheProductsViewModel()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateManageProductsUser() };
        var productsViewModel = new ProductsViewModel(new FakeProductManagementService());
        var viewModel = CreateViewModel(session: session, productsViewModel: productsViewModel);

        viewModel.SelectedNavigationItem = viewModel.NavigationItems.Single(i => i.Section == NavigationSection.Products);

        Assert.Same(productsViewModel, viewModel.CurrentViewModel);
    }

    [Fact]
    public void NavigatingToSalesAndBackToProductsKeepsTheSameChildViewModelInstances()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateManageProductsUser() };
        var salesViewModel = new SalesViewModel(session, new FakeCurrentRegisterSession(), new FakeSalesCartService(), new FakeProductManagementService(), new FakeCurrentSalesCart());
        var productsViewModel = new ProductsViewModel(new FakeProductManagementService());
        var viewModel = CreateViewModel(session: session, salesViewModel: salesViewModel, productsViewModel: productsViewModel);

        viewModel.SelectedNavigationItem = viewModel.NavigationItems.Single(i => i.Section == NavigationSection.Products);
        viewModel.SelectedNavigationItem = viewModel.NavigationItems.Single(i => i.Section == NavigationSection.Sales);

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
        var productsViewModel = new ProductsViewModel(new FakeProductManagementService());
        var viewModel = CreateViewModel(
            session: session, currentSalesCart: currentSalesCart, salesViewModel: salesViewModel, productsViewModel: productsViewModel);

        viewModel.SelectedNavigationItem = viewModel.NavigationItems.Single(i => i.Section == NavigationSection.Products);
        viewModel.SelectedNavigationItem = viewModel.NavigationItems.Single(i => i.Section == NavigationSection.Sales);

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
        var productsViewModel = new ProductsViewModel(productManagementService);
        var viewModel = CreateViewModel(session: session, productsViewModel: productsViewModel);

        productsViewModel.NewProductCommand.Execute(null);
        viewModel.ApplyProductCreated("SKU-NEW");

        Assert.Equal("SKU-NEW", productManagementService.LastSearchTerm);
    }

    [Fact]
    public void EditProductRequestedFromProductsBubblesUpWithTheProductId()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateManageProductsUser() };
        var productsViewModel = new ProductsViewModel(new CatalogFakeProductManagementService());
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
        ProductsViewModel? productsViewModel = null,
        RegisterViewModel? registerViewModel = null)
    {
        session ??= new FakeCurrentUserSession();
        registerSession ??= new FakeCurrentRegisterSession();
        currentSalesCart ??= new FakeCurrentSalesCart();
        dashboardViewModel ??= new DashboardViewModel(session, registerSession, currentSalesCart, new CatalogFakeProductManagementService());
        salesViewModel ??= new SalesViewModel(session, registerSession, new FakeSalesCartService(), new FakeProductManagementService(), currentSalesCart);
        productsViewModel ??= new ProductsViewModel(new CatalogFakeProductManagementService());
        var inventoryViewModel = new InventoryViewModel();
        registerViewModel ??= new RegisterViewModel(registerSession);
        var settingsViewModel = new SettingsViewModel();

        return new MainWindowViewModel(
            session, registerSession, currentSalesCart, dashboardViewModel, salesViewModel, productsViewModel,
            inventoryViewModel, registerViewModel, settingsViewModel);
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

    private static AuthenticatedUser CreateManageProductsUser() =>
        new(
            UserId.New(),
            OrganizationId.New(),
            RoleId.New(),
            "GERENTE",
            "Ana Pérez",
            "Gerente",
            [Permission.ProcessSale, Permission.ManageProducts]);

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
