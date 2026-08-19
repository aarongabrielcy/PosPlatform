using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Application.AdministrativeNotifications;
using Pos.Application.Authentication;
using Pos.Application.Common.Time;
using Pos.Application.ProductAudit;
using Pos.Application.Products.CreateProduct;
using Pos.Application.Products.ManageProduct;
using Pos.Application.RegisterSessions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Inventory;
using Pos.Domain.ProductAudit;
using Pos.Domain.Security;
using Pos.Infrastructure.Authentication;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Persistence.Repositories;
using Pos.Infrastructure.RegisterSessions;
using Pos.Infrastructure.Tests.Persistence;

namespace Pos.Infrastructure.Tests.Integration;

// Ejercita el ciclo de vida completo de la auditoría de productos (TAREA 24D) contra un SQLite
// real: Created -> Updated -> InventoryAdjusted -> Deactivated -> Activated, seguido de consultas
// del módulo administrativo global (filtros, paginación, orden, aislamiento por Organization).
public class ProductAuditSqliteIntegrationTests
{
    private static readonly DateTimeOffset SeedTimestamp = SqliteSeedHelper.DefaultTimestamp;

    // Clock determinístico: cada operación avanza un minuto exacto, evitando empates de
    // OccurredAtUtc que harían frágil la aserción de orden DESC.
    private sealed class SequentialClock : IClock
    {
        private DateTimeOffset _current;

        public SequentialClock(DateTimeOffset start) => _current = start;

        public DateTimeOffset UtcNow => _current;

        public void Advance(TimeSpan by) => _current += by;
    }

    private static PosDbContext CreateContext(SqliteConnection connection)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PosDbContext>();
        optionsBuilder.UseSqlite(connection);

        return new PosDbContext(optionsBuilder.Options);
    }

    [Fact]
    public async Task FullLifecycleCreatesOneAuditEventPerOperationQueryableInDescendingOrder()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        var context = CreateContext(connection);
        await using var contextDisposable = context;
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context, includeProduct: false);

        var userSession = new InMemoryCurrentUserSession();
        userSession.SetAuthenticatedUser(new AuthenticatedUser(
            new UserId(graph.UserId),
            new OrganizationId(graph.OrganizationId),
            new RoleId(graph.RoleId),
            "JPEREZ",
            "Juan Pérez",
            "Gerente",
            new HashSet<Permission>
            {
                Permission.ManageProducts, Permission.AdjustInventory, Permission.ViewProductAudit,
            }));

        var registerSession = new InMemoryCurrentRegisterSession();
        registerSession.SetActiveSession(new ActiveRegisterSession(
            new RegisterSessionId(graph.RegisterSessionId),
            new OrganizationId(graph.OrganizationId),
            new BranchId(graph.BranchId),
            new RegisterId(graph.RegisterId),
            "Caja 1",
            new UserId(graph.UserId),
            "Juan Pérez",
            SeedTimestamp,
            100m,
            "MXN"));

        var productRepository = new EfProductRepository(context);
        var inventoryItemRepository = new EfInventoryItemRepository(context);
        var inventoryMovementRepository = new EfInventoryMovementRepository(context);
        var productCatalogQuery = new EfProductCatalogQuery(context);
        var productAuditRepository = new EfProductAuditRepository(context);
        var productAuditQuery = new EfProductAuditQuery(context);
        var clock = new SequentialClock(SeedTimestamp);

        var createService = new CreateProductService(
            userSession, registerSession, productRepository, inventoryItemRepository, productAuditRepository,
            new Enforcement.FakeInstallationEnforcementStateService(), context, clock);

        var administrativeNotificationWriter = new AdministrativeNotificationWriter(
            new EfAdministrativeNotificationAudienceQuery(context), new EfAdministrativeNotificationRepository(context), clock);

        var managementService = new ProductManagementService(
            userSession, registerSession, productRepository, inventoryItemRepository, inventoryMovementRepository,
            productCatalogQuery, productAuditRepository, productAuditQuery, administrativeNotificationWriter,
            new Enforcement.FakeInstallationEnforcementStateService(), context, clock);

        // 1. Created
        var createResult = await createService.CreateAsync(new CreateProductRequest(
            "SKU-001", null, "Agua", null, 10m, null, true, 10m, 2m));
        Assert.True(createResult.Success);
        var productId = createResult.ProductId!.Value;

        // 2. Updated (2 changes)
        clock.Advance(TimeSpan.FromMinutes(1));
        var updateResult = await managementService.UpdateAsync(new UpdateProductRequest(
            productId, "SKU-001", null, "Agua Natural", null, 27.50m, null, null));
        Assert.True(updateResult.Success);

        // 3. InventoryAdjusted (10 -> 15)
        clock.Advance(TimeSpan.FromMinutes(1));
        var adjustResult = await managementService.AdjustInventoryAsync(
            new AdjustProductInventoryRequest(productId, InventoryAdjustmentType.Increase, 5m));
        Assert.True(adjustResult.Success);

        // 4. Deactivated
        clock.Advance(TimeSpan.FromMinutes(1));
        var deactivateResult = await managementService.SetActiveAsync(productId, false);
        Assert.True(deactivateResult.Success);

        // 5. Activated
        clock.Advance(TimeSpan.FromMinutes(1));
        var activateResult = await managementService.SetActiveAsync(productId, true);
        Assert.True(activateResult.Success);

        var organizationId = new OrganizationId(graph.OrganizationId);

        // ---------- Orden DESC global ----------
        var globalPage = await productAuditQuery.SearchPageAsync(
            organizationId, ProductAuditFilter.Empty, 0, 50, CancellationToken.None);

        Assert.Equal(5, globalPage.Items.Count);
        Assert.False(globalPage.HasNextPage);
        Assert.Equal(
            [
                ProductAuditAction.Activated,
                ProductAuditAction.Deactivated,
                ProductAuditAction.InventoryAdjusted,
                ProductAuditAction.Updated,
                ProductAuditAction.Created,
            ],
            globalPage.Items.Select(item => item.Action));

        // ---------- Actor y snapshots ----------
        var createdEntry = globalPage.Items.Single(item => item.Action == ProductAuditAction.Created);
        Assert.Equal(new UserId(graph.UserId), createdEntry.ActorUserId);
        Assert.Equal("JPEREZ", createdEntry.ActorUsername);
        Assert.Equal("Juan Pérez", createdEntry.ActorDisplayName);
        Assert.Equal(productId, createdEntry.ProductId);
        Assert.Equal("SKU-001", createdEntry.ProductSku);

        // ---------- Changes del evento Updated ----------
        var updatedEntry = globalPage.Items.Single(item => item.Action == ProductAuditAction.Updated);
        Assert.Equal(2, updatedEntry.Changes.Count);
        var nameChange = Assert.Single(updatedEntry.Changes, c => c.FieldName == ProductAuditField.Name);
        Assert.Equal("Agua", nameChange.OldValue);
        Assert.Equal("Agua Natural", nameChange.NewValue);

        // ---------- Filtro por ProductId ----------
        var byProduct = await productAuditQuery.SearchPageAsync(
            organizationId, new ProductAuditFilter(ProductId: productId), 0, 50, CancellationToken.None);
        Assert.Equal(5, byProduct.Items.Count);

        // ---------- Filtro por Action ----------
        var byAction = await productAuditQuery.SearchPageAsync(
            organizationId, new ProductAuditFilter(Action: ProductAuditAction.InventoryAdjusted), 0, 50,
            CancellationToken.None);
        var inventoryEntry = Assert.Single(byAction.Items);
        var quantityChange = Assert.Single(inventoryEntry.Changes);
        Assert.Equal(ProductAuditField.InventoryQuantity, quantityChange.FieldName);
        Assert.Equal("10", quantityChange.OldValue);
        Assert.Equal("15", quantityChange.NewValue);

        // ---------- Filtro por fecha (FromUtc excluye Created/Updated) ----------
        var byFromUtc = await productAuditQuery.SearchPageAsync(
            organizationId,
            new ProductAuditFilter(FromUtc: SeedTimestamp.AddMinutes(2)),
            0, 50, CancellationToken.None);
        Assert.Equal(3, byFromUtc.Items.Count);
        Assert.DoesNotContain(byFromUtc.Items, item => item.Action == ProductAuditAction.Created);
        Assert.DoesNotContain(byFromUtc.Items, item => item.Action == ProductAuditAction.Updated);

        // ---------- Paginación ----------
        var firstPage = await productAuditQuery.SearchPageAsync(
            organizationId, ProductAuditFilter.Empty, 0, 2, CancellationToken.None);
        Assert.Equal(2, firstPage.Items.Count);
        Assert.True(firstPage.HasNextPage);
        Assert.Equal(ProductAuditAction.Activated, firstPage.Items[0].Action);
        Assert.Equal(ProductAuditAction.Deactivated, firstPage.Items[1].Action);
    }

    [Fact]
    public async Task SearchPageAsyncNeverReturnsAuditEventsFromAnotherOrganization()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        var context = CreateContext(connection);
        await using var contextDisposable = context;
        await context.Database.EnsureCreatedAsync();

        var graphA = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context, includeProduct: false);
        var graphB = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context, includeProduct: false);

        var clock = new SequentialClock(SeedTimestamp);
        var productAuditRepository = new EfProductAuditRepository(context);
        var productAuditQuery = new EfProductAuditQuery(context);

        async Task CreateProductForGraphAsync(SqliteSeedHelper.CatalogGraph graph, string sku)
        {
            var userSession = new InMemoryCurrentUserSession();
            userSession.SetAuthenticatedUser(new AuthenticatedUser(
                new UserId(graph.UserId), new OrganizationId(graph.OrganizationId), new RoleId(graph.RoleId),
                "JPEREZ", "Juan Pérez", "Gerente", new HashSet<Permission> { Permission.ManageProducts }));

            var registerSession = new InMemoryCurrentRegisterSession();
            registerSession.SetActiveSession(new ActiveRegisterSession(
                new RegisterSessionId(graph.RegisterSessionId), new OrganizationId(graph.OrganizationId),
                new BranchId(graph.BranchId), new RegisterId(graph.RegisterId), "Caja 1",
                new UserId(graph.UserId), "Juan Pérez", SeedTimestamp, 100m, "MXN"));

            var service = new CreateProductService(
                userSession, registerSession, new EfProductRepository(context),
                new EfInventoryItemRepository(context), productAuditRepository,
                new Enforcement.FakeInstallationEnforcementStateService(), context, clock);

            var result = await service.CreateAsync(
                new CreateProductRequest(sku, null, "Producto", null, 10m, null, false, 0m, 0m),
                CancellationToken.None);
            Assert.True(result.Success);

            clock.Advance(TimeSpan.FromMinutes(1));
        }

        await CreateProductForGraphAsync(graphA, "SKU-A");
        await CreateProductForGraphAsync(graphB, "SKU-B");

        var pageA = await productAuditQuery.SearchPageAsync(
            new OrganizationId(graphA.OrganizationId), ProductAuditFilter.Empty, 0, 50, CancellationToken.None);

        Assert.Single(pageA.Items);
        Assert.Equal("SKU-A", pageA.Items[0].ProductSku);
    }

    [Fact]
    public async Task GetRecentActivityAsyncReturnsTheLatestNonCreatedEventPerProductWithinTheWindow()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        var context = CreateContext(connection);
        await using var contextDisposable = context;
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context, includeProduct: false);

        var userSession = new InMemoryCurrentUserSession();
        userSession.SetAuthenticatedUser(new AuthenticatedUser(
            new UserId(graph.UserId), new OrganizationId(graph.OrganizationId), new RoleId(graph.RoleId),
            "JPEREZ", "Juan Pérez", "Gerente",
            new HashSet<Permission> { Permission.ManageProducts, Permission.AdjustInventory }));

        var registerSession = new InMemoryCurrentRegisterSession();
        registerSession.SetActiveSession(new ActiveRegisterSession(
            new RegisterSessionId(graph.RegisterSessionId), new OrganizationId(graph.OrganizationId),
            new BranchId(graph.BranchId), new RegisterId(graph.RegisterId), "Caja 1",
            new UserId(graph.UserId), "Juan Pérez", SeedTimestamp, 100m, "MXN"));

        var productAuditRepository = new EfProductAuditRepository(context);
        var productAuditQuery = new EfProductAuditQuery(context);
        var clock = new SequentialClock(SeedTimestamp);

        var createService = new CreateProductService(
            userSession, registerSession, new EfProductRepository(context), new EfInventoryItemRepository(context),
            productAuditRepository, new Enforcement.FakeInstallationEnforcementStateService(), context, clock);

        var administrativeNotificationWriter = new AdministrativeNotificationWriter(
            new EfAdministrativeNotificationAudienceQuery(context), new EfAdministrativeNotificationRepository(context), clock);

        var managementService = new ProductManagementService(
            userSession, registerSession, new EfProductRepository(context), new EfInventoryItemRepository(context),
            new EfInventoryMovementRepository(context), new EfProductCatalogQuery(context), productAuditRepository,
            productAuditQuery, administrativeNotificationWriter,
            new Enforcement.FakeInstallationEnforcementStateService(), context, clock);

        var recentProductResult = await createService.CreateAsync(
            new CreateProductRequest("SKU-RECENT", null, "Reciente", null, 10m, null, true, 5m, 1m),
            CancellationToken.None);
        var recentProductId = recentProductResult.ProductId!.Value;

        clock.Advance(TimeSpan.FromHours(1));
        await managementService.SetActiveAsync(recentProductId, false);

        var oldProductResult = await createService.CreateAsync(
            new CreateProductRequest("SKU-OLD", null, "Antiguo", null, 10m, null, false, 0m, 0m),
            CancellationToken.None);
        var oldProductId = oldProductResult.ProductId!.Value;

        // El producto antiguo solo tiene Created (excluido de "reciente") dentro de la ventana.
        var organizationId = new OrganizationId(graph.OrganizationId);
        var sinceUtc = clock.UtcNow - TimeSpan.FromHours(24);

        var activity = await productAuditQuery.GetRecentActivityAsync(
            organizationId, [recentProductId, oldProductId], sinceUtc, CancellationToken.None);

        Assert.True(activity.ContainsKey(recentProductId));
        Assert.False(activity.ContainsKey(oldProductId));
        Assert.Equal(ProductAuditAction.Deactivated, activity[recentProductId].Action);
    }
}
