using Pos.Application.Authentication;
using Pos.Application.Inventory;
using Pos.Application.RegisterSessions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;

namespace Pos.Application.Tests.Inventory;

public class InventoryServiceTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    private sealed record Fixture(
        InventoryService Service,
        FakeInventoryCatalogQuery CatalogQuery,
        FakeInventoryMovementQuery MovementQuery,
        OrganizationId OrganizationId,
        BranchId BranchId);

    private static Fixture CreateFixture(
        bool authenticated = true,
        bool registerOpen = true,
        IEnumerable<Permission>? permissions = null,
        FakeInventoryCatalogQuery? catalogQuery = null,
        FakeInventoryMovementQuery? movementQuery = null)
    {
        var organizationId = OrganizationId.New();
        var branchId = BranchId.New();

        var userSession = new FakeCurrentUserSession();

        if (authenticated)
        {
            userSession.CurrentUser = new AuthenticatedUser(
                UserId.New(),
                organizationId,
                RoleId.New(),
                "JPEREZ",
                "Juan Pérez",
                "Gerente",
                permissions ?? [Permission.ViewInventory, Permission.AdjustInventory]);
        }

        var registerSession = new FakeCurrentRegisterSession();

        if (registerOpen)
        {
            registerSession.Current = new ActiveRegisterSession(
                RegisterSessionId.New(),
                organizationId,
                branchId,
                RegisterId.New(),
                "Caja 1",
                UserId.New(),
                "Juan Pérez",
                CreatedAtUtc,
                100m,
                "MXN");
        }

        catalogQuery ??= new FakeInventoryCatalogQuery();
        movementQuery ??= new FakeInventoryMovementQuery();

        var service = new InventoryService(userSession, registerSession, catalogQuery, movementQuery);

        return new Fixture(service, catalogQuery, movementQuery, organizationId, branchId);
    }

    // ---------- GetCatalogPageAsync ----------

    [Fact]
    public async Task GetCatalogPageAsyncReturnsEmptyWhenNotAuthenticated()
    {
        var fixture = CreateFixture(authenticated: false);

        var result = await fixture.Service.GetCatalogPageAsync(null, InventoryCatalogStatusFilter.All, 0, 50);

        Assert.Empty(result.Items);
        Assert.Equal(0, fixture.CatalogQuery.SearchPageCallCount);
    }

    [Fact]
    public async Task GetCatalogPageAsyncReturnsEmptyWithoutAnOpenRegisterSession()
    {
        var fixture = CreateFixture(registerOpen: false);

        var result = await fixture.Service.GetCatalogPageAsync(null, InventoryCatalogStatusFilter.All, 0, 50);

        Assert.Empty(result.Items);
        Assert.Equal(0, fixture.CatalogQuery.SearchPageCallCount);
    }

    [Fact]
    public async Task GetCatalogPageAsyncReturnsEmptyWithoutViewInventoryPermission()
    {
        var fixture = CreateFixture(permissions: [Permission.ProcessSale]);

        var result = await fixture.Service.GetCatalogPageAsync(null, InventoryCatalogStatusFilter.All, 0, 50);

        Assert.Empty(result.Items);
        Assert.Equal(0, fixture.CatalogQuery.SearchPageCallCount);
    }

    // READ-ONLY CORRECTION (sección 12 de la tarea): "Separate ViewInventory from AdjustInventory.
    // Queries → ViewInventory." AdjustInventory por sí solo (sin ViewInventory) ya no basta para
    // consultar - antes (TAREA 24G) cualquiera de los dos permisos bastaba.
    [Fact]
    public async Task GetCatalogPageAsyncReturnsEmptyWithOnlyAdjustInventoryPermission()
    {
        var fixture = CreateFixture(permissions: [Permission.AdjustInventory]);

        var result = await fixture.Service.GetCatalogPageAsync(null, InventoryCatalogStatusFilter.All, 0, 50);

        Assert.Empty(result.Items);
        Assert.Equal(0, fixture.CatalogQuery.SearchPageCallCount);
    }

    [Fact]
    public async Task GetCatalogPageAsyncDelegatesWithOnlyViewInventoryPermission()
    {
        var fixture = CreateFixture(permissions: [Permission.ViewInventory]);

        await fixture.Service.GetCatalogPageAsync("agua", InventoryCatalogStatusFilter.LowStock, 50, 25);

        Assert.Equal(1, fixture.CatalogQuery.SearchPageCallCount);
        Assert.Equal(fixture.OrganizationId, fixture.CatalogQuery.LastOrganizationId);
        Assert.Equal(fixture.BranchId, fixture.CatalogQuery.LastBranchId);
        Assert.Equal("agua", fixture.CatalogQuery.LastSearchTerm);
        Assert.Equal(InventoryCatalogStatusFilter.LowStock, fixture.CatalogQuery.LastFilter);
        Assert.Equal(50, fixture.CatalogQuery.LastSkip);
        Assert.Equal(25, fixture.CatalogQuery.LastTake);
    }

    // ---------- GetSummaryAsync ----------

    [Fact]
    public async Task GetSummaryAsyncReturnsEmptyWhenNotAuthenticated()
    {
        var fixture = CreateFixture(authenticated: false);

        var result = await fixture.Service.GetSummaryAsync();

        Assert.Equal(InventorySummary.Empty, result);
        Assert.Equal(0, fixture.CatalogQuery.GetSummaryCallCount);
    }

    [Fact]
    public async Task GetSummaryAsyncDelegatesToTheCatalogQueryWithTheCurrentOrganizationAndBranch()
    {
        var summary = new InventorySummary(10, 6, 3, 1);
        var fixture = CreateFixture(catalogQuery: new FakeInventoryCatalogQuery(summary: summary));

        var result = await fixture.Service.GetSummaryAsync();

        Assert.Equal(summary, result);
        Assert.Equal(1, fixture.CatalogQuery.GetSummaryCallCount);
        Assert.Equal(fixture.OrganizationId, fixture.CatalogQuery.LastOrganizationId);
        Assert.Equal(fixture.BranchId, fixture.CatalogQuery.LastBranchId);
    }

    // ---------- GetMovementPageAsync ----------

    [Fact]
    public async Task GetMovementPageAsyncReturnsEmptyWhenNotAuthenticated()
    {
        var fixture = CreateFixture(authenticated: false);

        var result = await fixture.Service.GetMovementPageAsync(InventoryMovementFilter.Empty, 0, 50);

        Assert.Empty(result.Items);
        Assert.Equal(0, fixture.MovementQuery.SearchPageCallCount);
    }

    [Fact]
    public async Task GetMovementPageAsyncReturnsEmptyWithoutViewInventoryPermission()
    {
        var fixture = CreateFixture(permissions: [Permission.ProcessSale]);

        var result = await fixture.Service.GetMovementPageAsync(InventoryMovementFilter.Empty, 0, 50);

        Assert.Empty(result.Items);
        Assert.Equal(0, fixture.MovementQuery.SearchPageCallCount);
    }

    [Fact]
    public async Task GetMovementPageAsyncDelegatesToTheMovementQueryWithTheCurrentOrganizationAndBranch()
    {
        var fixture = CreateFixture();
        var filter = new InventoryMovementFilter(ProductId: ProductId.New());

        await fixture.Service.GetMovementPageAsync(filter, 10, 20);

        Assert.Equal(1, fixture.MovementQuery.SearchPageCallCount);
        Assert.Equal(fixture.OrganizationId, fixture.MovementQuery.LastOrganizationId);
        Assert.Equal(fixture.BranchId, fixture.MovementQuery.LastBranchId);
        Assert.Equal(filter, fixture.MovementQuery.LastFilter);
        Assert.Equal(10, fixture.MovementQuery.LastSkip);
        Assert.Equal(20, fixture.MovementQuery.LastTake);
    }
}
