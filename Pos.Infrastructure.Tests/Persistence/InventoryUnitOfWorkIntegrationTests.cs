using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Inventory;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Persistence.Records;
using Pos.Infrastructure.Persistence.Repositories;

namespace Pos.Infrastructure.Tests.Persistence;

public class InventoryUnitOfWorkIntegrationTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset OccurredAtUtc = new(2026, 1, 2, 10, 0, 0, TimeSpan.Zero);

    private static PosDbContext CreateContext(SqliteConnection connection)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PosDbContext>();
        optionsBuilder.UseSqlite(connection);

        return new PosDbContext(optionsBuilder.Options);
    }

    [Fact]
    public async Task SingleCommitPersistsBothInventoryUpdateAndMovement()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var itemId = InventoryItemId.New();
        var branchId = BranchId.New();
        var productId = ProductId.New();
        var performedByUserId = UserId.New();

        // InventoryItem tiene FK Restrict hacia Branch y Product.
        await SqliteSeedHelper.SeedOrganizationBranchAndProductAsync(
            context, CreatedAtUtc, branchId: branchId.Value, productId: productId.Value);

        context.Add(new InventoryItemRecord
        {
            Id = itemId.Value,
            BranchId = branchId.Value,
            ProductId = productId.Value,
            Quantity = 10m,
            ReorderPoint = 2m,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = CreatedAtUtc,
        });
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var inventoryRepository = new EfInventoryItemRepository(context);
        var movementRepository = new EfInventoryMovementRepository(context);

        var item = await inventoryRepository.GetByBranchAndProductAsync(branchId, productId, CancellationToken.None);
        Assert.NotNull(item);

        var movement = InventoryMovement.CreateManualDecrease(
            InventoryMovementId.New(),
            item!.Id,
            item.BranchId,
            item.ProductId,
            performedByUserId,
            quantity: 4m,
            quantityBefore: item.Quantity,
            OccurredAtUtc);

        item.ApplyMovement(movement);

        await inventoryRepository.UpdateAsync(item, CancellationToken.None);
        await movementRepository.AddAsync(movement, CancellationToken.None);

        var itemEntry = context.ChangeTracker.Entries<InventoryItemRecord>().Single(e => e.Entity.Id == itemId.Value);
        var movementEntry = context.ChangeTracker.Entries<InventoryMovementRecord>()
            .Single(e => e.Entity.Id == movement.Id.Value);

        Assert.Equal(EntityState.Modified, itemEntry.State);
        Assert.Equal(EntityState.Added, movementEntry.State);

        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var reloadedItem = await context.Set<InventoryItemRecord>().SingleAsync(r => r.Id == itemId.Value);
        var reloadedMovement = await context.Set<InventoryMovementRecord>().SingleAsync(r => r.Id == movement.Id.Value);

        Assert.Equal(6m, reloadedItem.Quantity);
        Assert.Equal(OccurredAtUtc, reloadedItem.UpdatedAtUtc);

        Assert.Equal(10m, reloadedMovement.QuantityBefore);
        Assert.Equal(6m, reloadedMovement.QuantityAfter);
        Assert.Equal(4m, reloadedMovement.Quantity);
        Assert.Equal(InventoryMovementType.ManualDecrease, reloadedMovement.Type);
    }
}
