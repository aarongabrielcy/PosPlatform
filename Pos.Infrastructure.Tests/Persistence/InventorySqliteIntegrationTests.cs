using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Domain.Inventory;
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
}
