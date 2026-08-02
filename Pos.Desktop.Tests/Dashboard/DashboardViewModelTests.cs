using Pos.Application.Authentication;
using Pos.Application.Products.ManageProduct;
using Pos.Application.RegisterSessions;
using Pos.Application.SalesCart;
using Pos.Desktop.Dashboard;
using Pos.Desktop.Tests.Main;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;
using FakeProductManagementService = Pos.Desktop.Tests.Products.Catalog.FakeProductManagementService;

namespace Pos.Desktop.Tests.Dashboard;

public class DashboardViewModelTests
{
    private static readonly DateTimeOffset OpenedAtUtc = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    private static AuthenticatedUser CreateManageProductsUser() =>
        new(
            UserId.New(), OrganizationId.New(), RoleId.New(), "GERENTE", "Ana Pérez", "Gerente",
            [Permission.ProcessSale, Permission.ManageProducts]);

    private static AuthenticatedUser CreateCashierUser() =>
        new(
            UserId.New(), OrganizationId.New(), RoleId.New(), "CAJERO", "Ana Pérez", "Cajero",
            [Permission.ProcessSale]);

    private static ActiveRegisterSession CreateActiveRegisterSession() =>
        new(
            RegisterSessionId.New(), OrganizationId.New(), BranchId.New(), RegisterId.New(), "Caja 1",
            UserId.New(), "Ana Pérez", OpenedAtUtc, 100m, "MXN");

    private static ProductCatalogItem CreateLowStockItem(string sku = "SKU-LOW") =>
        new(ProductId.New(), sku, null, "Producto bajo stock", 10m, "MXN", true, 1m, 5m, true);

    [Fact]
    public async Task LoadCommandPopulatesTotalLowStockAndOutOfStockForAManageProductsUser()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateManageProductsUser() };
        var service = new FakeProductManagementService(
            summary: new ProductCatalogSummary(12, 3, 2));
        var viewModel = new DashboardViewModel(session, new FakeCurrentRegisterSession(), new FakeCurrentSalesCart(), service);

        viewModel.LoadCommand.Execute(null);
        await Task.Yield();

        Assert.True(viewModel.CanViewProductCards);
        Assert.Equal(12, viewModel.TotalProducts);
        Assert.Equal(3, viewModel.LowStockCount);
        Assert.Equal(2, viewModel.OutOfStockCount);
        Assert.Equal(1, service.GetDashboardSummaryCallCount);
    }

    [Fact]
    public async Task ProductCardsAreHiddenForAUserWithoutManageProductsPermission()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateCashierUser() };
        var service = new FakeProductManagementService(summary: new ProductCatalogSummary(12, 3, 2));
        var viewModel = new DashboardViewModel(session, new FakeCurrentRegisterSession(), new FakeCurrentSalesCart(), service);

        viewModel.LoadCommand.Execute(null);
        await Task.Yield();

        Assert.False(viewModel.CanViewProductCards);
        Assert.Equal(0, viewModel.TotalProducts);
        Assert.Equal(0, viewModel.LowStockCount);
        Assert.Equal(0, viewModel.OutOfStockCount);
        Assert.Equal(0, service.GetDashboardSummaryCallCount);
    }

    [Fact]
    public async Task LoadCommandPopulatesTheLowStockPreviewTable()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateManageProductsUser() };
        var lowStockItem = CreateLowStockItem();
        var service = new FakeProductManagementService(
            catalogHandler: (_, _, _, _, _) => Task.FromResult(new ProductCatalogPageResult([lowStockItem], false)));
        var viewModel = new DashboardViewModel(session, new FakeCurrentRegisterSession(), new FakeCurrentSalesCart(), service);

        viewModel.LoadCommand.Execute(null);
        await Task.Yield();

        Assert.Single(viewModel.LowStockItems);
        Assert.Equal(ProductCatalogStatusFilter.LowStock, service.LastFilter);
        Assert.Equal(string.Empty, viewModel.LowStockStatusMessage);
    }

    [Fact]
    public async Task LowStockStatusMessageShowsWhenThereAreNoLowStockProducts()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateManageProductsUser() };
        var service = new FakeProductManagementService();
        var viewModel = new DashboardViewModel(session, new FakeCurrentRegisterSession(), new FakeCurrentSalesCart(), service);

        viewModel.LoadCommand.Execute(null);
        await Task.Yield();

        Assert.Equal("No hay productos con stock bajo.", viewModel.LowStockStatusMessage);
    }

    [Fact]
    public async Task LoadCommandReflectsAnOpenRegisterSession()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateCashierUser() };
        var registerSession = new FakeCurrentRegisterSession { Current = CreateActiveRegisterSession() };
        var viewModel = new DashboardViewModel(session, registerSession, new FakeCurrentSalesCart(), new FakeProductManagementService());

        viewModel.LoadCommand.Execute(null);
        await Task.Yield();

        Assert.True(viewModel.IsRegisterOpen);
        Assert.Equal("Caja 1", viewModel.RegisterName);
        Assert.Equal("Abierta", viewModel.RegisterStatusText);
    }

    [Fact]
    public async Task LoadCommandReflectsAClosedRegisterSession()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateCashierUser() };
        var viewModel = new DashboardViewModel(session, new FakeCurrentRegisterSession(), new FakeCurrentSalesCart(), new FakeProductManagementService());

        viewModel.LoadCommand.Execute(null);
        await Task.Yield();

        Assert.False(viewModel.IsRegisterOpen);
        Assert.Equal("Cerrada", viewModel.RegisterStatusText);
    }

    [Fact]
    public async Task LoadCommandReflectsTheCurrentCartTotalAndLineCount()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateCashierUser() };
        var currentSalesCart = new FakeCurrentSalesCart();
        currentSalesCart.SetSnapshot(new SalesCartSnapshot(
            [new SalesCartLine(ProductId.New(), "SKU-001", "Agua 1L", 2m, 10m, 20m, "MXN", 5m, true)], "MXN"));
        var viewModel = new DashboardViewModel(session, new FakeCurrentRegisterSession(), currentSalesCart, new FakeProductManagementService());

        viewModel.LoadCommand.Execute(null);
        await Task.Yield();

        Assert.Equal(1, viewModel.CartLineCount);
        Assert.Contains("20", viewModel.CartTotalText);
        Assert.Contains("MXN", viewModel.CartTotalText);
    }

    // Reflejar el estado más reciente al recargar/activar (sección 21): no push en vivo, pero
    // volver a ejecutar LoadCommand debe recoger los cambios ocurridos en el carrito mientras
    // tanto.
    [Fact]
    public async Task ReloadingReflectsCartChangesMadeSinceTheLastLoad()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateCashierUser() };
        var currentSalesCart = new FakeCurrentSalesCart();
        var viewModel = new DashboardViewModel(session, new FakeCurrentRegisterSession(), currentSalesCart, new FakeProductManagementService());

        viewModel.LoadCommand.Execute(null);
        await Task.Yield();
        Assert.Equal(0, viewModel.CartLineCount);

        currentSalesCart.SetSnapshot(new SalesCartSnapshot(
            [new SalesCartLine(ProductId.New(), "SKU-001", "Agua 1L", 1m, 10m, 10m, "MXN", 5m, true)], "MXN"));

        viewModel.LoadCommand.Execute(null);
        await Task.Yield();

        Assert.Equal(1, viewModel.CartLineCount);
    }

    [Fact]
    public void ViewProductCommandCannotExecuteWithoutAnItem()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateManageProductsUser() };
        var viewModel = new DashboardViewModel(session, new FakeCurrentRegisterSession(), new FakeCurrentSalesCart(), new FakeProductManagementService());

        Assert.False(viewModel.ViewProductCommand.CanExecute(null));
    }

    [Fact]
    public void ViewProductCommandRaisesNavigateToProductsRequested()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateManageProductsUser() };
        var viewModel = new DashboardViewModel(session, new FakeCurrentRegisterSession(), new FakeCurrentSalesCart(), new FakeProductManagementService());

        var raised = false;
        viewModel.NavigateToProductsRequested += (_, _) => raised = true;

        viewModel.ViewProductCommand.Execute(CreateLowStockItem());

        Assert.True(raised);
    }
}
