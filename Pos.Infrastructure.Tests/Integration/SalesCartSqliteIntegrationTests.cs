using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Authentication;
using Pos.Application.RegisterSessions;
using Pos.Application.SalesCart;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;
using Pos.Infrastructure.Authentication;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Persistence.Records;
using Pos.Infrastructure.Persistence.Repositories;
using Pos.Infrastructure.RegisterSessions;
using Pos.Infrastructure.SalesCart;
using Pos.Infrastructure.Tests.Persistence;

namespace Pos.Infrastructure.Tests.Integration;

// Ejercita SalesCartService contra un SQLite real (bootstrap -> login -> caja abierta ->
// producto/inventario sembrados por fixtures existentes) para confirmar que buscar, agregar,
// actualizar cantidad y cancelar nunca crean una Sale ni modifican InventoryItem: el carrito vive
// únicamente en memoria durante toda esta fase.
public class SalesCartSqliteIntegrationTests
{
    private static PosDbContext CreateContext(SqliteConnection connection)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PosDbContext>();
        optionsBuilder.UseSqlite(connection);

        return new PosDbContext(optionsBuilder.Options);
    }

    private static async Task<(
        PosDbContext Context,
        SalesCartService Service,
        InMemoryCurrentSalesCart Cart,
        SqliteSeedHelper.CatalogGraph Graph)> CreateAuthenticatedServiceAsync(SqliteConnection connection)
    {
        var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context);
        await SqliteSeedHelper.SeedInventoryItemAsync(context, graph.BranchId, graph.ProductId, quantity: 10m);

        var userSession = new InMemoryCurrentUserSession();
        userSession.SetAuthenticatedUser(new AuthenticatedUser(
            new UserId(graph.UserId),
            new OrganizationId(graph.OrganizationId),
            new RoleId(graph.RoleId),
            "JPEREZ",
            "Juan Pérez",
            "Cajero",
            new HashSet<Permission>()));

        var registerSession = new InMemoryCurrentRegisterSession();
        registerSession.SetActiveSession(new ActiveRegisterSession(
            new RegisterSessionId(graph.RegisterSessionId),
            new OrganizationId(graph.OrganizationId),
            new BranchId(graph.BranchId),
            new RegisterId(graph.RegisterId),
            "Caja 1",
            new UserId(graph.UserId),
            "Juan Pérez",
            SqliteSeedHelper.DefaultTimestamp,
            100m,
            "MXN"));

        var cart = new InMemoryCurrentSalesCart();
        var productRepository = new EfProductRepository(context);
        var inventoryItemRepository = new EfInventoryItemRepository(context);

        var service = new SalesCartService(userSession, registerSession, cart, productRepository, inventoryItemRepository);

        return (context, service, cart, graph);
    }

    [Fact]
    public async Task SearchAddUpdateAndCancelNeverCreateASaleOrChangeInventory()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        var (context, service, cart, graph) = await CreateAuthenticatedServiceAsync(connection);
        await using var contextDisposable = context;

        var searchResults = await service.SearchProductsAsync("SKU-001");
        Assert.Single(searchResults);
        Assert.Equal(10m, searchResults[0].AvailableQuantity);

        var addResult = await service.AddProductAsync(new AddProductToCartRequest(new ProductId(graph.ProductId), 3m));
        Assert.True(addResult.Success);
        Assert.Equal(3m, cart.Snapshot.Lines[0].Quantity);

        await AssertNoSalePersistedAsync(context);
        await AssertInventoryQuantityAsync(context, graph, 10m);

        var updateResult = await service.UpdateQuantityAsync(
            new UpdateCartLineQuantityRequest(new ProductId(graph.ProductId), 5m));
        Assert.True(updateResult.Success);
        Assert.Equal(5m, cart.Snapshot.Lines[0].Quantity);

        await AssertNoSalePersistedAsync(context);
        await AssertInventoryQuantityAsync(context, graph, 10m);

        var clearResult = service.Clear();
        Assert.True(clearResult.Success);
        Assert.Empty(cart.Snapshot.Lines);

        await AssertNoSalePersistedAsync(context);
        await AssertInventoryQuantityAsync(context, graph, 10m);
    }

    private static async Task AssertNoSalePersistedAsync(PosDbContext context) =>
        Assert.Equal(0, await context.Set<SaleRecord>().CountAsync());

    private static async Task AssertInventoryQuantityAsync(
        PosDbContext context, SqliteSeedHelper.CatalogGraph graph, decimal expectedQuantity)
    {
        var record = await context.Set<InventoryItemRecord>()
            .AsNoTracking()
            .SingleAsync(r => r.BranchId == graph.BranchId && r.ProductId == graph.ProductId);

        Assert.Equal(expectedQuantity, record.Quantity);
    }
}
