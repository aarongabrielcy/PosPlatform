using Pos.Application.Authentication;
using Pos.Application.Sales.History;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;

namespace Pos.Application.Tests.Sales.History;

public class SalesHistoryServiceTests
{
    private sealed record Fixture(SalesHistoryService Service, FakeSalesHistoryQuery Query, OrganizationId OrganizationId);

    private static Fixture CreateFixture(
        bool authenticated = true, IEnumerable<Permission>? permissions = null, FakeSalesHistoryQuery? query = null)
    {
        var organizationId = OrganizationId.New();
        var session = new FakeCurrentUserSession();

        if (authenticated)
        {
            session.CurrentUser = new AuthenticatedUser(
                UserId.New(), organizationId, RoleId.New(), "GERENTE", "Ana Pérez", "Gerente",
                permissions ?? [Permission.ViewSalesHistory]);
        }

        query ??= new FakeSalesHistoryQuery();

        return new Fixture(new SalesHistoryService(session, query), query, organizationId);
    }

    // ---------- SearchPageAsync ----------

    [Fact]
    public async Task SearchPageAsyncReturnsEmptyWhenNotAuthenticated()
    {
        var fixture = CreateFixture(authenticated: false);

        var result = await fixture.Service.SearchPageAsync(SalesHistoryFilter.Empty, 0, 50);

        Assert.Empty(result.Items);
        Assert.Equal(0, fixture.Query.SearchPageCallCount);
    }

    [Fact]
    public async Task SearchPageAsyncReturnsEmptyWithoutViewSalesHistoryPermission()
    {
        var fixture = CreateFixture(permissions: [Permission.ProcessSale]);

        var result = await fixture.Service.SearchPageAsync(SalesHistoryFilter.Empty, 0, 50);

        Assert.Empty(result.Items);
        Assert.Equal(0, fixture.Query.SearchPageCallCount);
    }

    [Fact]
    public async Task SearchPageAsyncDelegatesToTheQueryWithTheCurrentOrganizationWhenAuthorized()
    {
        var fixture = CreateFixture();
        var filter = new SalesHistoryFilter(SearchTerm: "agua");

        await fixture.Service.SearchPageAsync(filter, 10, 25);

        Assert.Equal(1, fixture.Query.SearchPageCallCount);
        Assert.Equal(fixture.OrganizationId, fixture.Query.LastOrganizationId);
        Assert.Equal(filter, fixture.Query.LastFilter);
        Assert.Equal(10, fixture.Query.LastSkip);
        Assert.Equal(25, fixture.Query.LastTake);
    }

    // ---------- GetSummaryAsync ----------

    [Fact]
    public async Task GetSummaryAsyncReturnsEmptyWhenNotAuthenticated()
    {
        var fixture = CreateFixture(authenticated: false);

        var result = await fixture.Service.GetSummaryAsync(SalesHistoryFilter.Empty);

        Assert.Equal(SalesHistorySummary.Empty, result);
        Assert.Equal(0, fixture.Query.GetSummaryCallCount);
    }

    [Fact]
    public async Task GetSummaryAsyncReturnsEmptyWithoutViewSalesHistoryPermission()
    {
        var fixture = CreateFixture(permissions: [Permission.ProcessSale]);

        var result = await fixture.Service.GetSummaryAsync(SalesHistoryFilter.Empty);

        Assert.Equal(SalesHistorySummary.Empty, result);
        Assert.Equal(0, fixture.Query.GetSummaryCallCount);
    }

    [Fact]
    public async Task GetSummaryAsyncDelegatesToTheQueryWithTheCurrentOrganizationWhenAuthorized()
    {
        var fixture = CreateFixture();
        var filter = new SalesHistoryFilter(RegisterId: RegisterId.New());

        await fixture.Service.GetSummaryAsync(filter);

        Assert.Equal(1, fixture.Query.GetSummaryCallCount);
        Assert.Equal(fixture.OrganizationId, fixture.Query.LastSummaryOrganizationId);
        Assert.Equal(filter, fixture.Query.LastSummaryFilter);
    }

    // ---------- GetDetailAsync ----------

    [Fact]
    public async Task GetDetailAsyncReturnsNullWhenNotAuthenticated()
    {
        var fixture = CreateFixture(authenticated: false);

        var result = await fixture.Service.GetDetailAsync(SaleId.New());

        Assert.Null(result);
        Assert.Equal(0, fixture.Query.GetDetailCallCount);
    }

    [Fact]
    public async Task GetDetailAsyncReturnsNullWithoutViewSalesHistoryPermission()
    {
        var fixture = CreateFixture(permissions: [Permission.ProcessSale]);

        var result = await fixture.Service.GetDetailAsync(SaleId.New());

        Assert.Null(result);
        Assert.Equal(0, fixture.Query.GetDetailCallCount);
    }

    // Seguridad de detalle (TAREA 25B, sección 28): la Organization del usuario actual siempre
    // viaja hasta la consulta, nunca se confía en un SaleId "libre" desde Desktop.
    [Fact]
    public async Task GetDetailAsyncDelegatesWithTheCurrentOrganizationAndExactSaleIdWhenAuthorized()
    {
        var fixture = CreateFixture();
        var saleId = SaleId.New();

        await fixture.Service.GetDetailAsync(saleId);

        Assert.Equal(1, fixture.Query.GetDetailCallCount);
        Assert.Equal(fixture.OrganizationId, fixture.Query.LastDetailOrganizationId);
        Assert.Equal(saleId, fixture.Query.LastDetailSaleId);
    }

    // ---------- GetFilterOptionsAsync ----------

    [Fact]
    public async Task GetFilterOptionsAsyncReturnsEmptyWhenNotAuthenticated()
    {
        var fixture = CreateFixture(authenticated: false);

        var result = await fixture.Service.GetFilterOptionsAsync();

        Assert.Empty(result.Cashiers);
        Assert.Empty(result.Registers);
        Assert.Equal(0, fixture.Query.GetFilterOptionsCallCount);
    }

    [Fact]
    public async Task GetFilterOptionsAsyncReturnsEmptyWithoutViewSalesHistoryPermission()
    {
        var fixture = CreateFixture(permissions: [Permission.ProcessSale]);

        var result = await fixture.Service.GetFilterOptionsAsync();

        Assert.Empty(result.Cashiers);
        Assert.Empty(result.Registers);
        Assert.Equal(0, fixture.Query.GetFilterOptionsCallCount);
    }

    [Fact]
    public async Task GetFilterOptionsAsyncDelegatesToTheQueryWithTheCurrentOrganizationWhenAuthorized()
    {
        var fixture = CreateFixture();

        await fixture.Service.GetFilterOptionsAsync();

        Assert.Equal(1, fixture.Query.GetFilterOptionsCallCount);
        Assert.Equal(fixture.OrganizationId, fixture.Query.LastFilterOptionsOrganizationId);
    }

    // ---------- READ-ONLY CORRECTION: ViewSalesHistory != ViewReports (sección 14 de la tarea) ----------

    // Prueba explícita del desacople: Sales History ya NO es "lo mismo que" Administrative
    // Reports, así que ViewReports por sí solo (sin ViewSalesHistory) no debe autorizar ninguna
    // consulta de Historial.
    [Fact]
    public async Task SearchPageAsyncReturnsEmptyWithOnlyViewReportsPermission()
    {
        var fixture = CreateFixture(permissions: [Permission.ViewReports]);

        var result = await fixture.Service.SearchPageAsync(SalesHistoryFilter.Empty, 0, 50);

        Assert.Empty(result.Items);
        Assert.Equal(0, fixture.Query.SearchPageCallCount);
    }

    // Un Cashier (ProcessSale + ViewSalesHistory, sin ViewReports) sí puede consultar Historial y
    // abrir Sale Detail: matriz congelada de la tarea, sección 3/23.
    [Fact]
    public async Task SearchPageAsyncDelegatesForACashierWithOnlyViewSalesHistoryPermission()
    {
        var fixture = CreateFixture(permissions: [Permission.ProcessSale, Permission.ViewSalesHistory]);

        await fixture.Service.SearchPageAsync(SalesHistoryFilter.Empty, 0, 50);

        Assert.Equal(1, fixture.Query.SearchPageCallCount);
    }

    [Fact]
    public async Task GetDetailAsyncDelegatesForACashierWithOnlyViewSalesHistoryPermission()
    {
        var fixture = CreateFixture(permissions: [Permission.ProcessSale, Permission.ViewSalesHistory]);

        await fixture.Service.GetDetailAsync(SaleId.New());

        Assert.Equal(1, fixture.Query.GetDetailCallCount);
    }
}
