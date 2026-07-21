using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Domain.Inventory;
using Pos.Domain.Sales;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Tests.Persistence;

public class InventorySqliteIntegrationTests
{
    private static PosDbContext CreateContext(SqliteConnection connection)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PosDbContext>();
        optionsBuilder.UseSqlite(connection);

        return new PosDbContext(optionsBuilder.Options);
    }

    [Fact]
    public async Task InventoryItemAndMovementShouldRoundTripThroughSqlite()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var inventoryItemId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var performedByUserId = Guid.NewGuid();
        var createdAtUtc = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        var occurredAtUtc = new DateTimeOffset(2026, 1, 1, 9, 30, 0, TimeSpan.Zero);

        await SqliteSeedHelper.SeedOrganizationBranchAndProductAsync(
            context, createdAtUtc, branchId: branchId, productId: productId);

        var inventoryItem = new InventoryItemRecord
        {
            Id = inventoryItemId,
            BranchId = branchId,
            ProductId = productId,
            Quantity = 10.5m,
            ReorderPoint = 2.25m,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = createdAtUtc,
        };

        var inventoryMovement = new InventoryMovementRecord
        {
            Id = Guid.NewGuid(),
            InventoryItemId = inventoryItemId,
            BranchId = branchId,
            ProductId = productId,
            PerformedByUserId = performedByUserId,
            Type = InventoryMovementType.ManualDecrease,
            Quantity = 1.25m,
            QuantityBefore = 10.5m,
            QuantityAfter = 9.25m,
            SaleId = null,
            SaleLineId = null,
            OccurredAtUtc = occurredAtUtc,
        };

        context.Add(inventoryItem);
        context.Add(inventoryMovement);

        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var reloadedItem = await context.Set<InventoryItemRecord>().SingleAsync(record => record.Id == inventoryItemId);
        var reloadedMovement = await context.Set<InventoryMovementRecord>()
            .SingleAsync(record => record.Id == inventoryMovement.Id);

        Assert.Equal(inventoryItemId, reloadedItem.Id);
        Assert.Equal(branchId, reloadedItem.BranchId);
        Assert.Equal(productId, reloadedItem.ProductId);
        Assert.Equal(10.5m, reloadedItem.Quantity);
        Assert.Equal(2.25m, reloadedItem.ReorderPoint);
        Assert.Equal(createdAtUtc, reloadedItem.CreatedAtUtc);
        Assert.Equal(TimeSpan.Zero, reloadedItem.CreatedAtUtc.Offset);
        Assert.Equal(createdAtUtc.UtcTicks, reloadedItem.CreatedAtUtc.UtcTicks);

        Assert.Equal(inventoryMovement.Id, reloadedMovement.Id);
        Assert.Equal(inventoryItemId, reloadedMovement.InventoryItemId);
        Assert.Equal(InventoryMovementType.ManualDecrease, reloadedMovement.Type);
        Assert.Equal(1.25m, reloadedMovement.Quantity);
        Assert.Equal(10.5m, reloadedMovement.QuantityBefore);
        Assert.Equal(9.25m, reloadedMovement.QuantityAfter);
        Assert.Null(reloadedMovement.SaleId);
        Assert.Null(reloadedMovement.SaleLineId);
        Assert.Equal(occurredAtUtc, reloadedMovement.OccurredAtUtc);
        Assert.Equal(TimeSpan.Zero, reloadedMovement.OccurredAtUtc.Offset);
        Assert.Equal(occurredAtUtc.UtcTicks, reloadedMovement.OccurredAtUtc.UtcTicks);
    }

    [Fact]
    public async Task DeletingInventoryItemWithMovementsShouldFailDueToRestrictForeignKey()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var inventoryItemId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await SqliteSeedHelper.SeedOrganizationBranchAndProductAsync(
            context, now, branchId: branchId, productId: productId);

        var inventoryItem = new InventoryItemRecord
        {
            Id = inventoryItemId,
            BranchId = branchId,
            ProductId = productId,
            Quantity = 5m,
            ReorderPoint = 1m,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        var inventoryMovement = new InventoryMovementRecord
        {
            Id = Guid.NewGuid(),
            InventoryItemId = inventoryItemId,
            BranchId = branchId,
            ProductId = productId,
            PerformedByUserId = Guid.NewGuid(),
            Type = InventoryMovementType.ManualIncrease,
            Quantity = 5m,
            QuantityBefore = 0m,
            QuantityAfter = 5m,
            OccurredAtUtc = now,
        };

        context.AddRange(inventoryItem, inventoryMovement);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var itemToDelete = await context.Set<InventoryItemRecord>().SingleAsync(record => record.Id == inventoryItemId);
        context.Remove(itemToDelete);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.CommitAsync(CancellationToken.None));
    }

    [Fact]
    public async Task SavingInventoryItemWithDuplicateBranchAndProductShouldFailDueToUniqueIndex()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var branchId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await SqliteSeedHelper.SeedOrganizationBranchAndProductAsync(
            context, now, branchId: branchId, productId: productId);

        var firstItem = new InventoryItemRecord
        {
            Id = Guid.NewGuid(),
            BranchId = branchId,
            ProductId = productId,
            Quantity = 1m,
            ReorderPoint = 0m,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        context.Add(firstItem);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var duplicateItem = new InventoryItemRecord
        {
            Id = Guid.NewGuid(),
            BranchId = branchId,
            ProductId = productId,
            Quantity = 2m,
            ReorderPoint = 0m,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        context.Add(duplicateItem);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.CommitAsync(CancellationToken.None));
    }

    [Fact]
    public async Task InsertingInventoryItemWithNonExistentBranchShouldFailDueToForeignKey()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.UtcNow;
        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context, now);

        context.Add(new InventoryItemRecord
        {
            Id = Guid.NewGuid(),
            BranchId = Guid.NewGuid(),
            ProductId = graph.ProductId,
            Quantity = 1m,
            ReorderPoint = 0m,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.CommitAsync(CancellationToken.None));
    }

    [Fact]
    public async Task InsertingInventoryItemWithNonExistentProductShouldFailDueToForeignKey()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.UtcNow;
        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context, now);

        context.Add(new InventoryItemRecord
        {
            Id = Guid.NewGuid(),
            BranchId = graph.BranchId,
            ProductId = Guid.NewGuid(),
            Quantity = 1m,
            ReorderPoint = 0m,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.CommitAsync(CancellationToken.None));
    }

    [Fact]
    public async Task DeletingBranchWithInventoryItemShouldFailDueToRestrictForeignKey()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.UtcNow;
        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context, now);
        await SqliteSeedHelper.SeedInventoryItemAsync(context, graph.BranchId, graph.ProductId, createdAtUtc: now, updatedAtUtc: now);

        var branchToDelete = await context.Set<BranchRecord>().SingleAsync(r => r.Id == graph.BranchId);
        context.Remove(branchToDelete);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.CommitAsync(CancellationToken.None));
    }

    [Fact]
    public async Task DeletingProductWithInventoryItemShouldFailDueToRestrictForeignKey()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.UtcNow;
        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context, now);
        await SqliteSeedHelper.SeedInventoryItemAsync(context, graph.BranchId, graph.ProductId, createdAtUtc: now, updatedAtUtc: now);

        var productToDelete = await context.Set<ProductRecord>().SingleAsync(r => r.Id == graph.ProductId);
        context.Remove(productToDelete);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.CommitAsync(CancellationToken.None));
    }

    [Fact]
    public async Task InsertingSaleWithNonExistentOrganizationShouldFailDueToForeignKey()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.UtcNow;
        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context, now);

        context.Add(new SaleRecord
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            BranchId = graph.BranchId,
            RegisterSessionId = graph.RegisterSessionId,
            CreatedByUserId = graph.UserId,
            Currency = "MXN",
            Status = SaleStatus.Draft,
            CreatedAtUtc = now,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.CommitAsync(CancellationToken.None));
    }

    [Fact]
    public async Task InsertingSaleWithNonExistentBranchShouldFailDueToForeignKey()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.UtcNow;
        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context, now);

        context.Add(new SaleRecord
        {
            Id = Guid.NewGuid(),
            OrganizationId = graph.OrganizationId,
            BranchId = Guid.NewGuid(),
            RegisterSessionId = graph.RegisterSessionId,
            CreatedByUserId = graph.UserId,
            Currency = "MXN",
            Status = SaleStatus.Draft,
            CreatedAtUtc = now,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.CommitAsync(CancellationToken.None));
    }

    [Fact]
    public async Task InsertingSaleWithNonExistentRegisterSessionShouldFailDueToForeignKey()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.UtcNow;
        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context, now);

        context.Add(new SaleRecord
        {
            Id = Guid.NewGuid(),
            OrganizationId = graph.OrganizationId,
            BranchId = graph.BranchId,
            RegisterSessionId = Guid.NewGuid(),
            CreatedByUserId = graph.UserId,
            Currency = "MXN",
            Status = SaleStatus.Draft,
            CreatedAtUtc = now,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.CommitAsync(CancellationToken.None));
    }

    [Fact]
    public async Task InsertingSaleWithNonExistentUserShouldFailDueToForeignKey()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.UtcNow;
        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context, now);

        context.Add(new SaleRecord
        {
            Id = Guid.NewGuid(),
            OrganizationId = graph.OrganizationId,
            BranchId = graph.BranchId,
            RegisterSessionId = graph.RegisterSessionId,
            CreatedByUserId = Guid.NewGuid(),
            Currency = "MXN",
            Status = SaleStatus.Draft,
            CreatedAtUtc = now,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.CommitAsync(CancellationToken.None));
    }

    [Fact]
    public async Task InsertingSaleLineWithNonExistentProductShouldFailDueToForeignKey()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.UtcNow;
        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context, now);

        var saleRecord = new SaleRecord
        {
            Id = Guid.NewGuid(),
            OrganizationId = graph.OrganizationId,
            BranchId = graph.BranchId,
            RegisterSessionId = graph.RegisterSessionId,
            CreatedByUserId = graph.UserId,
            Currency = "MXN",
            Status = SaleStatus.Draft,
            CreatedAtUtc = now,
        };

        saleRecord.Lines.Add(new SaleLineRecord
        {
            Id = Guid.NewGuid(),
            SaleId = saleRecord.Id,
            ProductId = Guid.NewGuid(),
            ProductSku = "SKU-001",
            ProductName = "Producto de prueba",
            Quantity = 1m,
            UnitPriceAmount = 10m,
            Currency = "MXN",
            Sale = saleRecord,
        });

        context.Add(saleRecord);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.CommitAsync(CancellationToken.None));
    }

    [Fact]
    public async Task DeletingProductUsedBySaleLineShouldFailDueToRestrictForeignKey()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.UtcNow;
        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context, now);
        await SqliteSeedHelper.SeedSaleAsync(
            context, graph.OrganizationId, graph.BranchId, graph.RegisterSessionId, graph.UserId, graph.ProductId, createdAtUtc: now);

        var productToDelete = await context.Set<ProductRecord>().SingleAsync(r => r.Id == graph.ProductId);
        context.Remove(productToDelete);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.CommitAsync(CancellationToken.None));
    }

    [Fact]
    public async Task DeletingOrganizationWithSaleShouldFailDueToRestrictForeignKey()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.UtcNow;
        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context, now);
        await SqliteSeedHelper.SeedSaleAsync(
            context, graph.OrganizationId, graph.BranchId, graph.RegisterSessionId, graph.UserId, graph.ProductId, createdAtUtc: now);

        var organizationToDelete = await context.Set<OrganizationRecord>().SingleAsync(r => r.Id == graph.OrganizationId);
        context.Remove(organizationToDelete);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.CommitAsync(CancellationToken.None));
    }

    [Fact]
    public async Task DeletingBranchWithSaleShouldFailDueToRestrictForeignKey()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.UtcNow;
        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context, now);
        await SqliteSeedHelper.SeedSaleAsync(
            context, graph.OrganizationId, graph.BranchId, graph.RegisterSessionId, graph.UserId, graph.ProductId, createdAtUtc: now);

        var branchToDelete = await context.Set<BranchRecord>().SingleAsync(r => r.Id == graph.BranchId);
        context.Remove(branchToDelete);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.CommitAsync(CancellationToken.None));
    }

    [Fact]
    public async Task DeletingRegisterSessionWithSaleShouldFailDueToRestrictForeignKey()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.UtcNow;
        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context, now);
        await SqliteSeedHelper.SeedSaleAsync(
            context, graph.OrganizationId, graph.BranchId, graph.RegisterSessionId, graph.UserId, graph.ProductId, createdAtUtc: now);

        var sessionToDelete = await context.Set<RegisterSessionRecord>().SingleAsync(r => r.Id == graph.RegisterSessionId);
        context.Remove(sessionToDelete);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.CommitAsync(CancellationToken.None));
    }

    [Fact]
    public async Task DeletingUserWhoCreatedSaleShouldFailDueToRestrictForeignKey()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.UtcNow;
        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context, now);
        await SqliteSeedHelper.SeedSaleAsync(
            context, graph.OrganizationId, graph.BranchId, graph.RegisterSessionId, graph.UserId, graph.ProductId, createdAtUtc: now);

        var userToDelete = await context.Set<UserRecord>().SingleAsync(r => r.Id == graph.UserId);
        context.Remove(userToDelete);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.CommitAsync(CancellationToken.None));
    }

    [Fact]
    public async Task DeletingSaleLineWithInventoryMovementReferencingItShouldFailDueToRestrictForeignKey()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.UtcNow;
        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context, now);
        var lineId = Guid.NewGuid();
        var sale = await SqliteSeedHelper.SeedSaleAsync(
            context,
            graph.OrganizationId,
            graph.BranchId,
            graph.RegisterSessionId,
            graph.UserId,
            graph.ProductId,
            saleLineId: lineId,
            createdAtUtc: now);
        await SqliteSeedHelper.SeedInventoryItemAsync(context, graph.BranchId, graph.ProductId, createdAtUtc: now, updatedAtUtc: now);

        context.Add(new InventoryMovementRecord
        {
            Id = Guid.NewGuid(),
            InventoryItemId = (await context.Set<InventoryItemRecord>().SingleAsync()).Id,
            BranchId = graph.BranchId,
            ProductId = graph.ProductId,
            PerformedByUserId = graph.UserId,
            Type = InventoryMovementType.SaleDecrease,
            Quantity = 1m,
            QuantityBefore = 10m,
            QuantityAfter = 9m,
            SaleId = sale.Id,
            SaleLineId = lineId,
            OccurredAtUtc = now,
        });

        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var lineToDelete = await context.Set<SaleLineRecord>().SingleAsync(r => r.Id == lineId);
        context.Remove(lineToDelete);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.CommitAsync(CancellationToken.None));
    }

    [Fact]
    public async Task DeletingSaleWithInventoryMovementReferencingItShouldFailDueToRestrictForeignKey()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.UtcNow;
        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context, now);
        var lineId = Guid.NewGuid();
        var sale = await SqliteSeedHelper.SeedSaleAsync(
            context,
            graph.OrganizationId,
            graph.BranchId,
            graph.RegisterSessionId,
            graph.UserId,
            graph.ProductId,
            saleLineId: lineId,
            createdAtUtc: now);
        await SqliteSeedHelper.SeedInventoryItemAsync(context, graph.BranchId, graph.ProductId, createdAtUtc: now, updatedAtUtc: now);

        context.Add(new InventoryMovementRecord
        {
            Id = Guid.NewGuid(),
            InventoryItemId = (await context.Set<InventoryItemRecord>().SingleAsync()).Id,
            BranchId = graph.BranchId,
            ProductId = graph.ProductId,
            PerformedByUserId = graph.UserId,
            Type = InventoryMovementType.SaleDecrease,
            Quantity = 1m,
            QuantityBefore = 10m,
            QuantityAfter = 9m,
            SaleId = sale.Id,
            SaleLineId = lineId,
            OccurredAtUtc = now,
        });

        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var saleToDelete = await context.Set<SaleRecord>().SingleAsync(r => r.Id == sale.Id);
        context.Remove(saleToDelete);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.CommitAsync(CancellationToken.None));
    }
}
