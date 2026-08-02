using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Authentication;
using Pos.Application.Products.ManageProduct;
using Pos.Application.RegisterSessions;
using Pos.Application.SalesCart;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Inventory;
using Pos.Domain.Security;
using Pos.Infrastructure.Authentication;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Persistence.Records;
using Pos.Infrastructure.Persistence.Repositories;
using Pos.Infrastructure.RegisterSessions;
using Pos.Infrastructure.SalesCart;
using Pos.Infrastructure.Tests.Persistence;
using Pos.Infrastructure.Time;

namespace Pos.Infrastructure.Tests.Integration;

// Ejercita ProductManagementService contra un SQLite real: edición, ajuste de inventario y
// activar/desactivar, confirmando que SearchActiveAsync (usado por SalesCartService) y la
// búsqueda administrativa (que sí puede incluir inactivos) se comportan como se espera, sin crear
// ninguna Sale ni requerir migraciones.
public class ProductManagementSqliteIntegrationTests
{
    private static readonly DateTimeOffset DefaultTimestamp = SqliteSeedHelper.DefaultTimestamp;

    private static PosDbContext CreateContext(SqliteConnection connection)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PosDbContext>();
        optionsBuilder.UseSqlite(connection);

        return new PosDbContext(optionsBuilder.Options);
    }

    private static async Task<(
        PosDbContext Context,
        ProductManagementService ManagementService,
        SalesCartService CartService,
        SqliteSeedHelper.CatalogGraph Graph)> CreateAuthenticatedServicesAsync(SqliteConnection connection)
    {
        var context = CreateContext(connection);
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
            new HashSet<Permission> { Permission.ManageProducts, Permission.AdjustInventory }));

        var registerSession = new InMemoryCurrentRegisterSession();
        registerSession.SetActiveSession(new ActiveRegisterSession(
            new RegisterSessionId(graph.RegisterSessionId),
            new OrganizationId(graph.OrganizationId),
            new BranchId(graph.BranchId),
            new RegisterId(graph.RegisterId),
            "Caja 1",
            new UserId(graph.UserId),
            "Juan Pérez",
            DefaultTimestamp,
            100m,
            "MXN"));

        var productRepository = new EfProductRepository(context);
        var inventoryItemRepository = new EfInventoryItemRepository(context);
        var inventoryMovementRepository = new EfInventoryMovementRepository(context);
        var clock = new SystemClock();

        var managementService = new ProductManagementService(
            userSession, registerSession, productRepository, inventoryItemRepository,
            inventoryMovementRepository, context, clock);

        var cartService = new SalesCartService(
            userSession, registerSession, new InMemoryCurrentSalesCart(), productRepository, inventoryItemRepository);

        return (context, managementService, cartService, graph);
    }

    private static async Task<ProductId> CreateProductAsync(
        PosDbContext context,
        SqliteSeedHelper.CatalogGraph graph,
        string sku = "SKU-EDIT",
        bool tracksInventory = true,
        decimal initialQuantity = 10m,
        decimal reorderPoint = 2m)
    {
        var productRepository = new EfProductRepository(context);
        var inventoryItemRepository = new EfInventoryItemRepository(context);

        var userSession = new InMemoryCurrentUserSession();
        userSession.SetAuthenticatedUser(new AuthenticatedUser(
            new UserId(graph.UserId),
            new OrganizationId(graph.OrganizationId),
            new RoleId(graph.RoleId),
            "JPEREZ",
            "Juan Pérez",
            "Gerente",
            new HashSet<Permission> { Permission.ManageProducts }));

        var registerSession = new InMemoryCurrentRegisterSession();
        registerSession.SetActiveSession(new ActiveRegisterSession(
            new RegisterSessionId(graph.RegisterSessionId),
            new OrganizationId(graph.OrganizationId),
            new BranchId(graph.BranchId),
            new RegisterId(graph.RegisterId),
            "Caja 1",
            new UserId(graph.UserId),
            "Juan Pérez",
            DefaultTimestamp,
            100m,
            "MXN"));

        var createService = new Pos.Application.Products.CreateProduct.CreateProductService(
            userSession, registerSession, productRepository, inventoryItemRepository, context, new SystemClock());

        var request = new Pos.Application.Products.CreateProduct.CreateProductRequest(
            sku, "7501234560000", "Producto editable", "Descripción original", 10m, 5m,
            tracksInventory, initialQuantity, reorderPoint);

        var result = await createService.CreateAsync(request, CancellationToken.None);
        Assert.True(result.Success);

        return result.ProductId!.Value;
    }

    [Fact]
    public async Task UpdateAsyncPersistsNameBarcodeAndPriceChanges()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        var (context, managementService, _, graph) = await CreateAuthenticatedServicesAsync(connection);
        await using var contextDisposable = context;

        var productId = await CreateProductAsync(context, graph);

        var result = await managementService.UpdateAsync(new UpdateProductRequest(
            productId, "7509999999999", "Nombre actualizado", "Descripción actualizada", 25m, 12m, null));

        Assert.True(result.Success);

        var record = await context.Set<ProductRecord>().AsNoTracking().SingleAsync(r => r.Id == productId.Value);
        Assert.Equal("Nombre actualizado", record.Name);
        Assert.Equal("7509999999999", record.Barcode);
        Assert.Equal(25m, record.SalePriceAmount);
        Assert.Equal(12m, record.CostAmount);
    }

    [Fact]
    public async Task UpdateAsyncPersistsReorderPointWithoutChangingQuantity()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        var (context, managementService, _, graph) = await CreateAuthenticatedServicesAsync(connection);
        await using var contextDisposable = context;

        var productId = await CreateProductAsync(context, graph, initialQuantity: 9m, reorderPoint: 2m);

        var result = await managementService.UpdateAsync(new UpdateProductRequest(
            productId, null, "Producto editable", "Descripción original", 10m, 5m, 6m));

        Assert.True(result.Success);

        var record = await context.Set<InventoryItemRecord>().AsNoTracking()
            .SingleAsync(r => r.ProductId == productId.Value);
        Assert.Equal(6m, record.ReorderPoint);
        Assert.Equal(9m, record.Quantity);
    }

    [Fact]
    public async Task AdjustInventoryAsyncCreatesMovementAndUpdatesQuantityInASingleCommit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        var (context, managementService, _, graph) = await CreateAuthenticatedServicesAsync(connection);
        await using var contextDisposable = context;

        var productId = await CreateProductAsync(context, graph, initialQuantity: 10m);

        var result = await managementService.AdjustInventoryAsync(
            new AdjustProductInventoryRequest(productId, InventoryAdjustmentType.Increase, 5m));

        Assert.True(result.Success);
        Assert.Equal(15m, result.NewQuantity);

        var itemRecord = await context.Set<InventoryItemRecord>().AsNoTracking()
            .SingleAsync(r => r.ProductId == productId.Value);
        Assert.Equal(15m, itemRecord.Quantity);

        var movementRecord = await context.Set<InventoryMovementRecord>().AsNoTracking()
            .SingleAsync(r => r.ProductId == productId.Value);
        Assert.Equal(InventoryMovementType.ManualIncrease, movementRecord.Type);
        Assert.Equal(10m, movementRecord.QuantityBefore);
        Assert.Equal(15m, movementRecord.QuantityAfter);

        // Ninguna venta se crea al ajustar inventario.
        Assert.Equal(0, await context.Set<SaleRecord>().CountAsync());
    }

    [Fact]
    public async Task DeactivateThenReactivateRoundTripsThroughSearchWithoutDeletingTheProduct()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        var (context, managementService, cartService, graph) = await CreateAuthenticatedServicesAsync(connection);
        await using var contextDisposable = context;

        var productId = await CreateProductAsync(context, graph, sku: "SKU-TOGGLE");

        var deactivateResult = await managementService.SetActiveAsync(productId, false);
        Assert.True(deactivateResult.Success);

        var activeSearch = await cartService.SearchProductsAsync("SKU-TOGGLE");
        Assert.Empty(activeSearch);

        var adminSearchIncludingInactive = await managementService.SearchAsync("SKU-TOGGLE", includeInactive: true);
        Assert.Single(adminSearchIncludingInactive);
        Assert.False(adminSearchIncludingInactive[0].IsActive);

        var adminSearchExcludingInactive = await managementService.SearchAsync("SKU-TOGGLE", includeInactive: false);
        Assert.Empty(adminSearchExcludingInactive);

        var productStillExists = await context.Set<ProductRecord>().AsNoTracking().SingleOrDefaultAsync(r => r.Id == productId.Value);
        Assert.NotNull(productStillExists);

        var reactivateResult = await managementService.SetActiveAsync(productId, true);
        Assert.True(reactivateResult.Success);

        var activeSearchAfterReactivation = await cartService.SearchProductsAsync("SKU-TOGGLE");
        Assert.Single(activeSearchAfterReactivation);
    }

    [Fact]
    public async Task GetByIdAsyncReturnsUpdatedDetailsImmediatelyAfterUpdate()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        var (context, managementService, _, graph) = await CreateAuthenticatedServicesAsync(connection);
        await using var contextDisposable = context;

        var productId = await CreateProductAsync(context, graph);
        await managementService.UpdateAsync(new UpdateProductRequest(
            productId, null, "Nombre confirmado", null, 30m, null, null));

        var details = await managementService.GetByIdAsync(productId);

        Assert.NotNull(details);
        Assert.Equal("Nombre confirmado", details!.Name);
        Assert.Equal(30m, details.SalePriceAmount);
    }
}
