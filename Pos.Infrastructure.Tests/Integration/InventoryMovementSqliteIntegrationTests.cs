using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Inventory;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Inventory;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Persistence.Records;
using Pos.Infrastructure.Persistence.Repositories;
using Pos.Infrastructure.Tests.Persistence;

namespace Pos.Infrastructure.Tests.Integration;

// Ejercita EfInventoryMovementQuery contra SQLite real (TAREA 24G, sección 21/40): filtrado por
// producto/tipo/rango de fechas, orden más reciente primero, paginación y aislamiento por
// tenant/branch. Nunca reconstruye QuantityBefore/QuantityAfter: solo lee lo persistido.
public class InventoryMovementSqliteIntegrationTests
{
    private static readonly DateTimeOffset DefaultTimestamp = SqliteSeedHelper.DefaultTimestamp;

    private static PosDbContext CreateContext(SqliteConnection connection)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PosDbContext>();
        optionsBuilder.UseSqlite(connection);

        return new PosDbContext(optionsBuilder.Options);
    }

    private static ProductRecord AddProduct(PosDbContext context, Guid organizationId, Guid productId, string sku, string name)
    {
        var record = new ProductRecord
        {
            Id = productId,
            OrganizationId = organizationId,
            Sku = sku,
            Name = name,
            SalePriceAmount = 10m,
            SalePriceCurrency = "MXN",
            TracksInventory = true,
            IsActive = true,
            CreatedAtUtc = DefaultTimestamp,
        };

        context.Add(record);
        return record;
    }

    [Fact]
    public async Task SearchPageOrdersByOccurredAtDescendingWithIdAsStableTiebreaker()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context);
        var item = await SqliteSeedHelper.SeedInventoryItemAsync(context, graph.BranchId, graph.ProductId, quantity: 10m);

        var older = await SqliteSeedHelper.SeedInventoryMovementAsync(
            context, item.Id, graph.BranchId, graph.ProductId, graph.UserId,
            quantity: 1m, quantityBefore: 0m, quantityAfter: 1m,
            occurredAtUtc: DefaultTimestamp.AddHours(-2));
        var newer = await SqliteSeedHelper.SeedInventoryMovementAsync(
            context, item.Id, graph.BranchId, graph.ProductId, graph.UserId,
            quantity: 2m, quantityBefore: 1m, quantityAfter: 3m,
            occurredAtUtc: DefaultTimestamp.AddHours(-1));

        var query = new EfInventoryMovementQuery(context);
        var page = await query.SearchPageAsync(
            new OrganizationId(graph.OrganizationId), new BranchId(graph.BranchId),
            InventoryMovementFilter.Empty, 0, 50, CancellationToken.None);

        Assert.Equal([newer.Id, older.Id], page.Items.Select(i => i.Id.Value));
    }

    [Fact]
    public async Task SearchPageReturnsExactlyThePersistedQuantityBeforeAndAfter()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context);
        var item = await SqliteSeedHelper.SeedInventoryItemAsync(context, graph.BranchId, graph.ProductId, quantity: 7m);

        await SqliteSeedHelper.SeedInventoryMovementAsync(
            context, item.Id, graph.BranchId, graph.ProductId, graph.UserId,
            type: InventoryMovementType.ManualDecrease, quantity: 3m, quantityBefore: 10m, quantityAfter: 7m);

        var query = new EfInventoryMovementQuery(context);
        var page = await query.SearchPageAsync(
            new OrganizationId(graph.OrganizationId), new BranchId(graph.BranchId),
            InventoryMovementFilter.Empty, 0, 50, CancellationToken.None);

        var movement = Assert.Single(page.Items);
        Assert.Equal(InventoryMovementType.ManualDecrease, movement.Type);
        Assert.Equal(3m, movement.Quantity);
        Assert.Equal(10m, movement.QuantityBefore);
        Assert.Equal(7m, movement.QuantityAfter);
    }

    [Fact]
    public async Task FilterByProductIdReturnsOnlyThatProductsMovements()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context, includeProduct: false);

        var productA = AddProduct(context, graph.OrganizationId, Guid.NewGuid(), "SKU-A", "Producto A");
        var productB = AddProduct(context, graph.OrganizationId, Guid.NewGuid(), "SKU-B", "Producto B");
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var itemA = await SqliteSeedHelper.SeedInventoryItemAsync(context, graph.BranchId, productA.Id, quantity: 5m);
        var itemB = await SqliteSeedHelper.SeedInventoryItemAsync(context, graph.BranchId, productB.Id, quantity: 5m);

        await SqliteSeedHelper.SeedInventoryMovementAsync(context, itemA.Id, graph.BranchId, productA.Id, graph.UserId);
        await SqliteSeedHelper.SeedInventoryMovementAsync(context, itemB.Id, graph.BranchId, productB.Id, graph.UserId);

        var query = new EfInventoryMovementQuery(context);
        var page = await query.SearchPageAsync(
            new OrganizationId(graph.OrganizationId), new BranchId(graph.BranchId),
            new InventoryMovementFilter(ProductId: new ProductId(productA.Id)), 0, 50, CancellationToken.None);

        var movement = Assert.Single(page.Items);
        Assert.Equal(productA.Id, movement.ProductId.Value);
    }

    [Fact]
    public async Task FilterByTypeAndDateRangeNarrowsResults()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context);
        var item = await SqliteSeedHelper.SeedInventoryItemAsync(context, graph.BranchId, graph.ProductId, quantity: 5m);

        var inWindowIncrease = await SqliteSeedHelper.SeedInventoryMovementAsync(
            context, item.Id, graph.BranchId, graph.ProductId, graph.UserId,
            type: InventoryMovementType.ManualIncrease, occurredAtUtc: DefaultTimestamp.AddDays(1));
        await SqliteSeedHelper.SeedInventoryMovementAsync(
            context, item.Id, graph.BranchId, graph.ProductId, graph.UserId,
            type: InventoryMovementType.ManualDecrease, occurredAtUtc: DefaultTimestamp.AddDays(1));
        await SqliteSeedHelper.SeedInventoryMovementAsync(
            context, item.Id, graph.BranchId, graph.ProductId, graph.UserId,
            type: InventoryMovementType.ManualIncrease, occurredAtUtc: DefaultTimestamp.AddDays(10));

        var query = new EfInventoryMovementQuery(context);
        var page = await query.SearchPageAsync(
            new OrganizationId(graph.OrganizationId), new BranchId(graph.BranchId),
            new InventoryMovementFilter(
                Type: InventoryMovementType.ManualIncrease,
                FromUtc: DefaultTimestamp,
                ToUtc: DefaultTimestamp.AddDays(2)),
            0, 50, CancellationToken.None);

        var movement = Assert.Single(page.Items);
        Assert.Equal(inWindowIncrease.Id, movement.Id.Value);
    }

    [Fact]
    public async Task SearchPageHonorsPageSizeAndSetsHasNextPageWithoutDuplicates()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context);
        var item = await SqliteSeedHelper.SeedInventoryItemAsync(context, graph.BranchId, graph.ProductId, quantity: 5m);

        var ids = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            var movement = await SqliteSeedHelper.SeedInventoryMovementAsync(
                context, item.Id, graph.BranchId, graph.ProductId, graph.UserId,
                occurredAtUtc: DefaultTimestamp.AddMinutes(i));
            ids.Add(movement.Id);
        }

        var query = new EfInventoryMovementQuery(context);

        var firstPage = await query.SearchPageAsync(
            new OrganizationId(graph.OrganizationId), new BranchId(graph.BranchId),
            InventoryMovementFilter.Empty, skip: 0, take: 2, CancellationToken.None);

        Assert.Equal(2, firstPage.Items.Count);
        Assert.True(firstPage.HasNextPage);

        var lastPage = await query.SearchPageAsync(
            new OrganizationId(graph.OrganizationId), new BranchId(graph.BranchId),
            InventoryMovementFilter.Empty, skip: 4, take: 2, CancellationToken.None);

        Assert.Single(lastPage.Items);
        Assert.False(lastPage.HasNextPage);
    }

    [Fact]
    public async Task SearchPageIsIsolatedByOrganizationAndBranch()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graphA = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context);
        var itemA = await SqliteSeedHelper.SeedInventoryItemAsync(context, graphA.BranchId, graphA.ProductId, quantity: 5m);
        await SqliteSeedHelper.SeedInventoryMovementAsync(context, itemA.Id, graphA.BranchId, graphA.ProductId, graphA.UserId);

        var graphB = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context);
        var itemB = await SqliteSeedHelper.SeedInventoryItemAsync(context, graphB.BranchId, graphB.ProductId, quantity: 5m);
        await SqliteSeedHelper.SeedInventoryMovementAsync(context, itemB.Id, graphB.BranchId, graphB.ProductId, graphB.UserId);

        var query = new EfInventoryMovementQuery(context);
        var page = await query.SearchPageAsync(
            new OrganizationId(graphA.OrganizationId), new BranchId(graphA.BranchId),
            InventoryMovementFilter.Empty, 0, 50, CancellationToken.None);

        var movement = Assert.Single(page.Items);
        Assert.Equal(graphA.ProductId, movement.ProductId.Value);
    }
}
