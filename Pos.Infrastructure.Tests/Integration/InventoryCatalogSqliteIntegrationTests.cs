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

// Ejercita EfInventoryCatalogQuery/EfInventoryMovementQuery contra SQLite real (TAREA 24G, sección
// 40): filtrado, estado de stock, paginación, búsqueda, orden, aislamiento por tenant/branch y
// visibilidad de productos inactivos con historial. Sin migraciones: el esquema ya existe.
public class InventoryCatalogSqliteIntegrationTests
{
    private static readonly DateTimeOffset DefaultTimestamp = SqliteSeedHelper.DefaultTimestamp;

    private static PosDbContext CreateContext(SqliteConnection connection)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PosDbContext>();
        optionsBuilder.UseSqlite(connection);

        return new PosDbContext(optionsBuilder.Options);
    }

    private static ProductRecord AddProduct(
        PosDbContext context, Guid organizationId, Guid productId, string sku, string name,
        bool tracksInventory = true, bool isActive = true, string? barcode = null)
    {
        var record = new ProductRecord
        {
            Id = productId,
            OrganizationId = organizationId,
            Sku = sku,
            Barcode = barcode,
            Name = name,
            SalePriceAmount = 10m,
            SalePriceCurrency = "MXN",
            TracksInventory = tracksInventory,
            IsActive = isActive,
            CreatedAtUtc = DefaultTimestamp,
        };

        context.Add(record);
        return record;
    }

    [Fact]
    public async Task SearchPageOnlyIncludesProductsThatTrackInventory()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedOrganizationBranchAndProductAsync(context, includeProduct: false);

        var tracked = AddProduct(context, graph.OrganizationId, Guid.NewGuid(), "SKU-TRACK", "Producto rastreado");
        var untracked = AddProduct(context, graph.OrganizationId, Guid.NewGuid(), "SKU-NOTRACK", "Producto sin rastreo", tracksInventory: false);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        await SqliteSeedHelper.SeedInventoryItemAsync(context, graph.BranchId, tracked.Id, quantity: 5m, reorderPoint: 2m);

        var query = new EfInventoryCatalogQuery(context);
        var page = await query.SearchPageAsync(
            new OrganizationId(graph.OrganizationId), new BranchId(graph.BranchId), null,
            InventoryCatalogStatusFilter.All, 0, 50, CancellationToken.None);

        Assert.Single(page.Items);
        Assert.Equal(tracked.Id, page.Items[0].ProductId.Value);
        Assert.DoesNotContain(page.Items, i => i.ProductId.Value == untracked.Id);
    }

    [Fact]
    public async Task StockStatusFollowsReorderPointThresholds()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedOrganizationBranchAndProductAsync(context, includeProduct: false);

        var outOfStock = AddProduct(context, graph.OrganizationId, Guid.NewGuid(), "SKU-OUT", "Sin existencia");
        var lowStockAtOne = AddProduct(context, graph.OrganizationId, Guid.NewGuid(), "SKU-LOW1", "Stock bajo 1");
        var lowStockAtReorder = AddProduct(context, graph.OrganizationId, Guid.NewGuid(), "SKU-LOW5", "Stock bajo 5");
        var inStock = AddProduct(context, graph.OrganizationId, Guid.NewGuid(), "SKU-IN", "Con stock");
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        // ReorderPoint = 5 (TAREA 24G, sección 36): 0 -> OutOfStock, 1 -> LowStock, 5 -> LowStock, 6 -> InStock.
        await SqliteSeedHelper.SeedInventoryItemAsync(context, graph.BranchId, outOfStock.Id, quantity: 0m, reorderPoint: 5m);
        await SqliteSeedHelper.SeedInventoryItemAsync(context, graph.BranchId, lowStockAtOne.Id, quantity: 1m, reorderPoint: 5m);
        await SqliteSeedHelper.SeedInventoryItemAsync(context, graph.BranchId, lowStockAtReorder.Id, quantity: 5m, reorderPoint: 5m);
        await SqliteSeedHelper.SeedInventoryItemAsync(context, graph.BranchId, inStock.Id, quantity: 6m, reorderPoint: 5m);

        var query = new EfInventoryCatalogQuery(context);
        var page = await query.SearchPageAsync(
            new OrganizationId(graph.OrganizationId), new BranchId(graph.BranchId), null,
            InventoryCatalogStatusFilter.All, 0, 50, CancellationToken.None);

        Assert.Equal(InventoryStockStatus.OutOfStock, Find(page, outOfStock.Id).StockStatus);
        Assert.Equal(InventoryStockStatus.LowStock, Find(page, lowStockAtOne.Id).StockStatus);
        Assert.Equal(InventoryStockStatus.LowStock, Find(page, lowStockAtReorder.Id).StockStatus);
        Assert.Equal(InventoryStockStatus.InStock, Find(page, inStock.Id).StockStatus);
    }

    [Fact]
    public async Task ReorderPointZeroNeverProducesLowStockForPositiveQuantity()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedOrganizationBranchAndProductAsync(context, includeProduct: false);

        var outOfStock = AddProduct(context, graph.OrganizationId, Guid.NewGuid(), "SKU-Z-OUT", "Sin existencia reorder 0");
        var inStock = AddProduct(context, graph.OrganizationId, Guid.NewGuid(), "SKU-Z-IN", "Con stock reorder 0");
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        // ReorderPoint = 0 (TAREA 24G, sección 8): 0 -> OutOfStock, 1 -> InStock (nunca LowStock).
        await SqliteSeedHelper.SeedInventoryItemAsync(context, graph.BranchId, outOfStock.Id, quantity: 0m, reorderPoint: 0m);
        await SqliteSeedHelper.SeedInventoryItemAsync(context, graph.BranchId, inStock.Id, quantity: 1m, reorderPoint: 0m);

        var query = new EfInventoryCatalogQuery(context);
        var page = await query.SearchPageAsync(
            new OrganizationId(graph.OrganizationId), new BranchId(graph.BranchId), null,
            InventoryCatalogStatusFilter.All, 0, 50, CancellationToken.None);

        Assert.Equal(InventoryStockStatus.OutOfStock, Find(page, outOfStock.Id).StockStatus);
        Assert.Equal(InventoryStockStatus.InStock, Find(page, inStock.Id).StockStatus);
    }

    [Fact]
    public async Task ProductWithoutAnInventoryItemRowIsTreatedAsOutOfStock()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedOrganizationBranchAndProductAsync(context, includeProduct: false);
        var product = AddProduct(context, graph.OrganizationId, Guid.NewGuid(), "SKU-NOITEM", "Sin InventoryItem aún");
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var query = new EfInventoryCatalogQuery(context);
        var page = await query.SearchPageAsync(
            new OrganizationId(graph.OrganizationId), new BranchId(graph.BranchId), null,
            InventoryCatalogStatusFilter.All, 0, 50, CancellationToken.None);

        var item = Find(page, product.Id);
        Assert.Equal(InventoryStockStatus.OutOfStock, item.StockStatus);
        Assert.Equal(0m, item.Quantity);
    }

    [Fact]
    public async Task InactiveProductWithInventoryRemainsVisible()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedOrganizationBranchAndProductAsync(context, includeProduct: false);
        var inactive = AddProduct(context, graph.OrganizationId, Guid.NewGuid(), "SKU-INACTIVE", "Descontinuado", isActive: false);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        await SqliteSeedHelper.SeedInventoryItemAsync(context, graph.BranchId, inactive.Id, quantity: 3m, reorderPoint: 1m);

        var query = new EfInventoryCatalogQuery(context);
        var page = await query.SearchPageAsync(
            new OrganizationId(graph.OrganizationId), new BranchId(graph.BranchId), null,
            InventoryCatalogStatusFilter.All, 0, 50, CancellationToken.None);

        var item = Find(page, inactive.Id);
        Assert.False(item.IsActive);
        Assert.Equal(3m, item.Quantity);
    }

    [Theory]
    [InlineData("SKU-BEBIDA")]
    [InlineData("789000111222")]
    [InlineData("agua")]
    public async Task SearchMatchesSkuBarcodeOrName(string term)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedOrganizationBranchAndProductAsync(context, includeProduct: false);
        var target = AddProduct(
            context, graph.OrganizationId, Guid.NewGuid(), "SKU-BEBIDA", "Agua mineral 1L",
            barcode: "789000111222");
        var other = AddProduct(context, graph.OrganizationId, Guid.NewGuid(), "SKU-OTRO", "Refresco de cola");
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        await SqliteSeedHelper.SeedInventoryItemAsync(context, graph.BranchId, target.Id, quantity: 5m, reorderPoint: 1m);
        await SqliteSeedHelper.SeedInventoryItemAsync(context, graph.BranchId, other.Id, quantity: 5m, reorderPoint: 1m);

        var query = new EfInventoryCatalogQuery(context);
        var page = await query.SearchPageAsync(
            new OrganizationId(graph.OrganizationId), new BranchId(graph.BranchId), term,
            InventoryCatalogStatusFilter.All, 0, 50, CancellationToken.None);

        Assert.Single(page.Items);
        Assert.Equal(target.Id, page.Items[0].ProductId.Value);
    }

    [Fact]
    public async Task SearchPageHonorsPageSizeAndSetsHasNextPageWithoutDuplicates()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedOrganizationBranchAndProductAsync(context, includeProduct: false);

        var productIds = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            var product = AddProduct(context, graph.OrganizationId, Guid.NewGuid(), $"SKU-{i:00}", $"Producto {i:00}");
            productIds.Add(product.Id);
        }

        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        foreach (var productId in productIds)
        {
            await SqliteSeedHelper.SeedInventoryItemAsync(context, graph.BranchId, productId, quantity: 5m, reorderPoint: 1m);
        }

        var query = new EfInventoryCatalogQuery(context);

        var firstPage = await query.SearchPageAsync(
            new OrganizationId(graph.OrganizationId), new BranchId(graph.BranchId), null,
            InventoryCatalogStatusFilter.All, skip: 0, take: 2, CancellationToken.None);

        Assert.Equal(2, firstPage.Items.Count);
        Assert.True(firstPage.HasNextPage);

        var lastPage = await query.SearchPageAsync(
            new OrganizationId(graph.OrganizationId), new BranchId(graph.BranchId), null,
            InventoryCatalogStatusFilter.All, skip: 4, take: 2, CancellationToken.None);

        Assert.Single(lastPage.Items);
        Assert.False(lastPage.HasNextPage);

        var allIds = firstPage.Items.Select(i => i.ProductId.Value)
            .Concat((await query.SearchPageAsync(
                new OrganizationId(graph.OrganizationId), new BranchId(graph.BranchId), null,
                InventoryCatalogStatusFilter.All, skip: 2, take: 2, CancellationToken.None)).Items.Select(i => i.ProductId.Value))
            .Concat(lastPage.Items.Select(i => i.ProductId.Value))
            .ToList();

        Assert.Equal(5, allIds.Distinct().Count());
    }

    [Fact]
    public async Task DefaultOrderingShowsOutOfStockThenLowStockThenInStock()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedOrganizationBranchAndProductAsync(context, includeProduct: false);

        var inStock = AddProduct(context, graph.OrganizationId, Guid.NewGuid(), "SKU-Z-IN", "Z Con stock");
        var outOfStock = AddProduct(context, graph.OrganizationId, Guid.NewGuid(), "SKU-A-OUT", "A Sin existencia");
        var lowStock = AddProduct(context, graph.OrganizationId, Guid.NewGuid(), "SKU-M-LOW", "M Stock bajo");
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        await SqliteSeedHelper.SeedInventoryItemAsync(context, graph.BranchId, inStock.Id, quantity: 10m, reorderPoint: 2m);
        await SqliteSeedHelper.SeedInventoryItemAsync(context, graph.BranchId, outOfStock.Id, quantity: 0m, reorderPoint: 2m);
        await SqliteSeedHelper.SeedInventoryItemAsync(context, graph.BranchId, lowStock.Id, quantity: 1m, reorderPoint: 2m);

        var query = new EfInventoryCatalogQuery(context);
        var page = await query.SearchPageAsync(
            new OrganizationId(graph.OrganizationId), new BranchId(graph.BranchId), null,
            InventoryCatalogStatusFilter.All, 0, 50, CancellationToken.None);

        Assert.Equal(
            [outOfStock.Id, lowStock.Id, inStock.Id],
            page.Items.Select(i => i.ProductId.Value));
    }

    [Fact]
    public async Task SummaryCountsAreScopedToOrganizationAndBranchAndExcludeUntracked()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedOrganizationBranchAndProductAsync(context, includeProduct: false);

        var inStock = AddProduct(context, graph.OrganizationId, Guid.NewGuid(), "SKU-A", "A");
        var lowStock = AddProduct(context, graph.OrganizationId, Guid.NewGuid(), "SKU-B", "B");
        var outOfStock = AddProduct(context, graph.OrganizationId, Guid.NewGuid(), "SKU-C", "C");
        var untracked = AddProduct(context, graph.OrganizationId, Guid.NewGuid(), "SKU-D", "D", tracksInventory: false);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        await SqliteSeedHelper.SeedInventoryItemAsync(context, graph.BranchId, inStock.Id, quantity: 10m, reorderPoint: 5m);
        await SqliteSeedHelper.SeedInventoryItemAsync(context, graph.BranchId, lowStock.Id, quantity: 5m, reorderPoint: 5m);
        await SqliteSeedHelper.SeedInventoryItemAsync(context, graph.BranchId, outOfStock.Id, quantity: 0m, reorderPoint: 5m);

        var query = new EfInventoryCatalogQuery(context);
        var summary = await query.GetSummaryAsync(
            new OrganizationId(graph.OrganizationId), new BranchId(graph.BranchId), CancellationToken.None);

        Assert.Equal(3, summary.TrackedProductsCount);
        Assert.Equal(1, summary.InStockCount);
        Assert.Equal(1, summary.LowStockCount);
        Assert.Equal(1, summary.OutOfStockCount);
        Assert.DoesNotContain(untracked.Id, new[] { inStock.Id, lowStock.Id, outOfStock.Id });
    }

    [Fact]
    public async Task SearchPageIsIsolatedByBranch()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedOrganizationBranchAndProductAsync(context, includeProduct: false);
        var otherBranchId = Guid.NewGuid();
        context.Add(new BranchRecord
        {
            Id = otherBranchId,
            OrganizationId = graph.OrganizationId,
            Name = "Sucursal Norte",
            Code = "SUC-2",
            IsActive = true,
            CreatedAtUtc = DefaultTimestamp,
        });

        var product = AddProduct(context, graph.OrganizationId, Guid.NewGuid(), "SKU-SHARED", "Producto compartido");
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        await SqliteSeedHelper.SeedInventoryItemAsync(context, otherBranchId, product.Id, quantity: 20m, reorderPoint: 1m);

        var query = new EfInventoryCatalogQuery(context);
        var page = await query.SearchPageAsync(
            new OrganizationId(graph.OrganizationId), new BranchId(graph.BranchId), null,
            InventoryCatalogStatusFilter.All, 0, 50, CancellationToken.None);

        // El producto trackea inventario y pertenece a la organización, pero su InventoryItem es de
        // otra Branch: debe aparecer con Quantity=0 (sin existencia en ESTA sucursal), no con la
        // cantidad de la otra sucursal.
        var item = Find(page, product.Id);
        Assert.Equal(0m, item.Quantity);
    }

    private static InventoryCatalogItem Find(InventoryCatalogPageResult page, Guid productId) =>
        page.Items.Single(i => i.ProductId.Value == productId);
}
