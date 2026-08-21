using Pos.Application.Authentication;
using Pos.Application.Inventory;
using Pos.Application.Reports;
using Pos.Domain.Branches;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;
using FakeBranchRepository = Pos.Application.Tests.Bootstrap.FakeBranchRepository;
using FakeInventoryCatalogQuery = Pos.Application.Tests.Inventory.FakeInventoryCatalogQuery;

namespace Pos.Application.Tests.Reports;

public class OperationalReportsServiceTests
{
    private static readonly DateTimeOffset FromUtc = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ToUtcExclusive = new(2026, 6, 2, 0, 0, 0, TimeSpan.Zero);

    private sealed record Fixture(
        OperationalReportsService Service,
        FakeOperationalReportsQuery Query,
        FakeInventoryCatalogQuery InventoryCatalogQuery,
        FakeBranchRepository BranchRepository,
        OrganizationId OrganizationId);

    private static Fixture CreateFixture(
        bool authenticated = true,
        IEnumerable<Permission>? permissions = null,
        FakeOperationalReportsQuery? query = null,
        FakeInventoryCatalogQuery? inventoryCatalogQuery = null,
        IEnumerable<Branch>? branches = null)
    {
        var organizationId = OrganizationId.New();
        var session = new FakeCurrentUserSession();

        if (authenticated)
        {
            session.CurrentUser = new AuthenticatedUser(
                UserId.New(), organizationId, RoleId.New(), "GERENTE", "Ana Pérez", "Gerente",
                permissions ?? [Permission.ViewReports]);
        }

        query ??= new FakeOperationalReportsQuery();
        inventoryCatalogQuery ??= new FakeInventoryCatalogQuery();
        var branchRepository = new FakeBranchRepository(
            branches ?? [new Branch(BranchId.New(), organizationId, "Sucursal Centro", "SUC-1", DateTimeOffset.UtcNow)]);

        var service = new OperationalReportsService(session, query, inventoryCatalogQuery, branchRepository);

        return new Fixture(service, query, inventoryCatalogQuery, branchRepository, organizationId);
    }

    // ---------- GetSalesSummaryAsync ----------

    [Fact]
    public async Task GetSalesSummaryAsyncReturnsEmptyWhenNotAuthenticated()
    {
        var fixture = CreateFixture(authenticated: false);

        var result = await fixture.Service.GetSalesSummaryAsync(FromUtc, ToUtcExclusive);

        Assert.Equal(SalesSummaryReport.Empty, result);
        Assert.Equal(0, fixture.Query.GetSalesSummaryCallCount);
    }

    [Fact]
    public async Task GetSalesSummaryAsyncReturnsEmptyWithoutViewReportsPermission()
    {
        var fixture = CreateFixture(permissions: [Permission.ProcessSale]);

        var result = await fixture.Service.GetSalesSummaryAsync(FromUtc, ToUtcExclusive);

        Assert.Equal(SalesSummaryReport.Empty, result);
        Assert.Equal(0, fixture.Query.GetSalesSummaryCallCount);
    }

    // Cashier no debe ver reportes aunque tenga ViewSalesHistory (sección 6/29 de la tarea).
    [Fact]
    public async Task GetSalesSummaryAsyncReturnsEmptyForACashierWithoutViewReports()
    {
        var fixture = CreateFixture(permissions: [Permission.ProcessSale, Permission.ViewSalesHistory]);

        var result = await fixture.Service.GetSalesSummaryAsync(FromUtc, ToUtcExclusive);

        Assert.Equal(SalesSummaryReport.Empty, result);
        Assert.Equal(0, fixture.Query.GetSalesSummaryCallCount);
    }

    [Fact]
    public async Task GetSalesSummaryAsyncDelegatesToTheQueryWithTheCurrentOrganizationWhenAuthorized()
    {
        var fixture = CreateFixture();

        await fixture.Service.GetSalesSummaryAsync(FromUtc, ToUtcExclusive);

        Assert.Equal(1, fixture.Query.GetSalesSummaryCallCount);
        Assert.Equal(fixture.OrganizationId, fixture.Query.LastOrganizationId);
        Assert.Equal(FromUtc, fixture.Query.LastFromUtc);
        Assert.Equal(ToUtcExclusive, fixture.Query.LastToUtcExclusive);
    }

    // Sección 23: From > To se rechaza sin llegar a la consulta.
    [Fact]
    public async Task GetSalesSummaryAsyncReturnsEmptyWhenFromIsAfterTo()
    {
        var fixture = CreateFixture();

        var result = await fixture.Service.GetSalesSummaryAsync(ToUtcExclusive, FromUtc);

        Assert.Equal(SalesSummaryReport.Empty, result);
        Assert.Equal(0, fixture.Query.GetSalesSummaryCallCount);
    }

    // ---------- GetRegisterClosuresAsync / GetRegisterClosureDetailAsync ----------

    [Fact]
    public async Task GetRegisterClosuresAsyncReturnsEmptyWithoutViewReportsPermission()
    {
        var fixture = CreateFixture(permissions: [Permission.CloseRegisterSession]);

        var result = await fixture.Service.GetRegisterClosuresAsync(FromUtc, ToUtcExclusive);

        Assert.Empty(result);
        Assert.Equal(0, fixture.Query.GetRegisterClosuresCallCount);
    }

    [Fact]
    public async Task GetRegisterClosuresAsyncDelegatesWhenAuthorized()
    {
        var fixture = CreateFixture();

        await fixture.Service.GetRegisterClosuresAsync(FromUtc, ToUtcExclusive);

        Assert.Equal(1, fixture.Query.GetRegisterClosuresCallCount);
        Assert.Equal(fixture.OrganizationId, fixture.Query.LastOrganizationId);
    }

    [Fact]
    public async Task GetRegisterClosureDetailAsyncReturnsNullWithoutViewReportsPermission()
    {
        var fixture = CreateFixture(permissions: [Permission.CloseRegisterSession]);

        var result = await fixture.Service.GetRegisterClosureDetailAsync(RegisterSessionId.New());

        Assert.Null(result);
        Assert.Equal(0, fixture.Query.GetRegisterClosureDetailCallCount);
    }

    [Fact]
    public async Task GetRegisterClosureDetailAsyncDelegatesWithTheExactSessionIdWhenAuthorized()
    {
        var fixture = CreateFixture();
        var registerSessionId = RegisterSessionId.New();

        await fixture.Service.GetRegisterClosureDetailAsync(registerSessionId);

        Assert.Equal(1, fixture.Query.GetRegisterClosureDetailCallCount);
        Assert.Equal(fixture.OrganizationId, fixture.Query.LastOrganizationId);
        Assert.Equal(registerSessionId, fixture.Query.LastRegisterSessionId);
    }

    // ---------- GetCashMovementsAsync ----------

    [Fact]
    public async Task GetCashMovementsAsyncReturnsEmptyWithoutViewReportsPermission()
    {
        var fixture = CreateFixture(permissions: [Permission.ManageCashMovements]);

        var result = await fixture.Service.GetCashMovementsAsync(FromUtc, ToUtcExclusive);

        Assert.Equal(CashMovementsReportResult.Empty, result);
        Assert.Equal(0, fixture.Query.GetCashMovementsCallCount);
    }

    [Fact]
    public async Task GetCashMovementsAsyncDelegatesWhenAuthorized()
    {
        var fixture = CreateFixture();

        await fixture.Service.GetCashMovementsAsync(FromUtc, ToUtcExclusive);

        Assert.Equal(1, fixture.Query.GetCashMovementsCallCount);
    }

    // ---------- GetProductSalesAsync ----------

    [Fact]
    public async Task GetProductSalesAsyncReturnsEmptyWithoutViewReportsPermission()
    {
        var fixture = CreateFixture(permissions: [Permission.ManageProducts]);

        var result = await fixture.Service.GetProductSalesAsync(FromUtc, ToUtcExclusive);

        Assert.Empty(result);
        Assert.Equal(0, fixture.Query.GetProductSalesCallCount);
    }

    [Fact]
    public async Task GetProductSalesAsyncDelegatesWhenAuthorized()
    {
        var fixture = CreateFixture();

        await fixture.Service.GetProductSalesAsync(FromUtc, ToUtcExclusive);

        Assert.Equal(1, fixture.Query.GetProductSalesCallCount);
    }

    // ---------- GetOperatorActivityAsync ----------

    [Fact]
    public async Task GetOperatorActivityAsyncReturnsEmptyWithoutViewReportsPermission()
    {
        var fixture = CreateFixture(permissions: [Permission.ProcessSale]);

        var result = await fixture.Service.GetOperatorActivityAsync(FromUtc, ToUtcExclusive);

        Assert.Empty(result);
        Assert.Equal(0, fixture.Query.GetOperatorActivityCallCount);
    }

    [Fact]
    public async Task GetOperatorActivityAsyncDelegatesWhenAuthorized()
    {
        var fixture = CreateFixture();

        await fixture.Service.GetOperatorActivityAsync(FromUtc, ToUtcExclusive);

        Assert.Equal(1, fixture.Query.GetOperatorActivityCallCount);
    }

    // ---------- GetLowStockAsync ----------

    [Fact]
    public async Task GetLowStockAsyncReturnsEmptyWithoutViewReportsPermission()
    {
        var fixture = CreateFixture(permissions: [Permission.ViewInventory]);

        var result = await fixture.Service.GetLowStockAsync();

        Assert.Empty(result);
        Assert.Equal(0, fixture.InventoryCatalogQuery.SearchPageCallCount);
    }

    // Sección 16/28: Low Stock no depende de una caja abierta (a diferencia de InventoryService),
    // así que un Manager sin sesión de caja activa puede consultarlo igual.
    [Fact]
    public async Task GetLowStockAsyncResolvesTheSingleActiveBranchWithoutRequiringAnOpenRegisterSession()
    {
        var fixture = CreateFixture();

        await fixture.Service.GetLowStockAsync();

        Assert.Equal(2, fixture.InventoryCatalogQuery.SearchPageCallCount);
        Assert.Equal(fixture.OrganizationId, fixture.InventoryCatalogQuery.LastOrganizationId);
    }

    [Fact]
    public async Task GetLowStockAsyncReturnsEmptyWhenNoActiveBranchExists()
    {
        var organizationId = OrganizationId.New();
        var inactiveBranch = Branch.Rehydrate(
            BranchId.New(), organizationId, "Sucursal Centro", "SUC-1", isActive: false, DateTimeOffset.UtcNow);
        var fixture = CreateFixture(branches: [inactiveBranch]);

        var result = await fixture.Service.GetLowStockAsync();

        Assert.Empty(result);
        Assert.Equal(0, fixture.InventoryCatalogQuery.SearchPageCallCount);
    }

    // El fake devuelve el mismo resultado fijo para ambas llamadas (OutOfStock y LowStock, sección
    // 16 de la tarea: dos consultas reutilizando IInventoryCatalogQuery, nunca una segunda
    // definición de umbral); esto verifica que GetLowStockAsync concatena ambas respuestas en vez de
    // quedarse solo con la última.
    [Fact]
    public async Task GetLowStockAsyncCombinesTheResultsOfBothStatusQueries()
    {
        var item = new InventoryCatalogItem(
            ProductId.New(), "SKU-LOW", null, "Stock bajo", true, 2m, 5m, InventoryStockStatus.LowStock);
        var query = new FakeInventoryCatalogQuery(new InventoryCatalogPageResult([item], false));
        var fixture = CreateFixture(inventoryCatalogQuery: query);

        var result = await fixture.Service.GetLowStockAsync();

        Assert.Equal(2, query.SearchPageCallCount);
        Assert.Equal(2, result.Count);
    }
}
