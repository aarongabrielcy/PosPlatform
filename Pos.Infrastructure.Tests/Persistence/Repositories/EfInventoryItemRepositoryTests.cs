using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Exceptions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Inventory;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Persistence.Records;
using Pos.Infrastructure.Persistence.Repositories;
using Pos.Infrastructure.Tests.Persistence;

namespace Pos.Infrastructure.Tests.Persistence.Repositories;

public class EfInventoryItemRepositoryTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset UpdatedAtUtc = new(2026, 1, 2, 9, 0, 0, TimeSpan.Zero);

    private static PosDbContext CreateContext(SqliteConnection connection)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PosDbContext>();
        optionsBuilder.UseSqlite(connection);

        return new PosDbContext(optionsBuilder.Options);
    }

    // InventoryItem tiene FK Restrict hacia Branch y Product: se siembra primero
    // Organization -> Branch -> Product con los mismos identificadores antes del InventoryItem.
    private static async Task<InventoryItemRecord> SeedItemAsync(
        PosDbContext context,
        Guid id,
        Guid branchId,
        Guid productId,
        decimal quantity = 10m,
        decimal reorderPoint = 2m)
    {
        await SqliteSeedHelper.SeedOrganizationBranchAndProductAsync(
            context, CreatedAtUtc, branchId: branchId, productId: productId);

        return await SqliteSeedHelper.SeedInventoryItemAsync(
            context, branchId, productId, id, quantity, reorderPoint, CreatedAtUtc, CreatedAtUtc);
    }

    [Fact]
    public async Task GetByBranchAndProductAsyncReturnsNullWhenNotFound()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var repository = new EfInventoryItemRepository(context);

        var result = await repository.GetByBranchAndProductAsync(BranchId.New(), ProductId.New(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByBranchAndProductAsyncFindsSeededItem()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var branchId = BranchId.New();
        var productId = ProductId.New();
        await SeedItemAsync(context, Guid.NewGuid(), branchId.Value, productId.Value);

        var repository = new EfInventoryItemRepository(context);
        var result = await repository.GetByBranchAndProductAsync(branchId, productId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(branchId, result!.BranchId);
        Assert.Equal(productId, result.ProductId);
    }

    [Fact]
    public async Task GetByBranchAndProductAsyncReconstructsAllValues()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var itemId = Guid.NewGuid();
        var branchId = BranchId.New();
        var productId = ProductId.New();
        await SeedItemAsync(context, itemId, branchId.Value, productId.Value, quantity: 7.5m, reorderPoint: 1.5m);

        var repository = new EfInventoryItemRepository(context);
        var result = await repository.GetByBranchAndProductAsync(branchId, productId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(itemId, result!.Id.Value);
        Assert.Equal(7.5m, result.Quantity);
        Assert.Equal(1.5m, result.ReorderPoint);
        Assert.Equal(CreatedAtUtc, result.CreatedAtUtc);
        Assert.Equal(CreatedAtUtc, result.UpdatedAtUtc);
    }

    [Fact]
    public async Task GetByBranchAndProductAsyncKeepsUpdatedAtUtcDistinctFromCreatedAtUtc()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var branchId = BranchId.New();
        var productId = ProductId.New();
        await SqliteSeedHelper.SeedOrganizationBranchAndProductAsync(
            context, CreatedAtUtc, branchId: branchId.Value, productId: productId.Value);
        await SqliteSeedHelper.SeedInventoryItemAsync(
            context, branchId.Value, productId.Value, Guid.NewGuid(), 10m, 2m, CreatedAtUtc, UpdatedAtUtc);

        var repository = new EfInventoryItemRepository(context);
        var result = await repository.GetByBranchAndProductAsync(branchId, productId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(CreatedAtUtc, result!.CreatedAtUtc);
        Assert.Equal(UpdatedAtUtc, result.UpdatedAtUtc);
    }

    [Fact]
    public async Task GetByBranchAndProductAsyncDoesNotTrackTheReturnedRecord()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var branchId = BranchId.New();
        var productId = ProductId.New();
        await SeedItemAsync(context, Guid.NewGuid(), branchId.Value, productId.Value);

        var repository = new EfInventoryItemRepository(context);
        await repository.GetByBranchAndProductAsync(branchId, productId, CancellationToken.None);

        Assert.Empty(context.ChangeTracker.Entries<InventoryItemRecord>());
    }

    [Fact]
    public async Task GetByBranchAndProductAsyncPropagatesCancellation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var repository = new EfInventoryItemRepository(context);
        using var cancelledSource = new CancellationTokenSource();
        cancelledSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => repository.GetByBranchAndProductAsync(BranchId.New(), ProductId.New(), cancelledSource.Token));
    }

    // ---------- AddAsync ----------

    [Fact]
    public async Task AddAsyncDoesNotPersistBeforeCommit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var branchId = BranchId.New();
        var productId = ProductId.New();
        await SqliteSeedHelper.SeedOrganizationBranchAndProductAsync(
            context, CreatedAtUtc, branchId: branchId.Value, productId: productId.Value);

        var item = new InventoryItem(
            InventoryItemId.New(), branchId, productId, 10m, 2m, CreatedAtUtc);
        var repository = new EfInventoryItemRepository(context);

        await repository.AddAsync(item, CancellationToken.None);
        context.ChangeTracker.Clear();

        var exists = await context.Set<InventoryItemRecord>().AnyAsync(r => r.Id == item.Id.Value);
        Assert.False(exists);
    }

    [Fact]
    public async Task AddAsyncPersistsInventoryItemAfterCommit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var branchId = BranchId.New();
        var productId = ProductId.New();
        await SqliteSeedHelper.SeedOrganizationBranchAndProductAsync(
            context, CreatedAtUtc, branchId: branchId.Value, productId: productId.Value);

        var item = new InventoryItem(
            InventoryItemId.New(), branchId, productId, 10m, 2m, CreatedAtUtc);
        var repository = new EfInventoryItemRepository(context);

        await repository.AddAsync(item, CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var reloaded = await repository.GetByBranchAndProductAsync(branchId, productId, CancellationToken.None);
        Assert.NotNull(reloaded);
        Assert.Equal(10m, reloaded!.Quantity);
        Assert.Equal(2m, reloaded.ReorderPoint);
    }

    [Fact]
    public async Task AddAsyncRejectsNullInventoryItem()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var repository = new EfInventoryItemRepository(context);

        await Assert.ThrowsAsync<ArgumentNullException>(() => repository.AddAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAsyncModifiesQuantity()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var branchId = BranchId.New();
        var productId = ProductId.New();
        await SeedItemAsync(context, Guid.NewGuid(), branchId.Value, productId.Value, quantity: 10m);

        var repository = new EfInventoryItemRepository(context);
        var item = await repository.GetByBranchAndProductAsync(branchId, productId, CancellationToken.None);
        item!.Increase(5m, UpdatedAtUtc);

        await repository.UpdateAsync(item, CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var reloaded = await context.Set<InventoryItemRecord>().SingleAsync(r => r.Id == item.Id.Value);
        Assert.Equal(15m, reloaded.Quantity);
    }

    [Fact]
    public async Task UpdateAsyncModifiesReorderPoint()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var branchId = BranchId.New();
        var productId = ProductId.New();
        await SeedItemAsync(context, Guid.NewGuid(), branchId.Value, productId.Value, reorderPoint: 2m);

        var repository = new EfInventoryItemRepository(context);
        var item = await repository.GetByBranchAndProductAsync(branchId, productId, CancellationToken.None);
        item!.ChangeReorderPoint(9m, UpdatedAtUtc);

        await repository.UpdateAsync(item, CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var reloaded = await context.Set<InventoryItemRecord>().SingleAsync(r => r.Id == item.Id.Value);
        Assert.Equal(9m, reloaded.ReorderPoint);
    }

    [Fact]
    public async Task UpdateAsyncModifiesUpdatedAtUtc()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var branchId = BranchId.New();
        var productId = ProductId.New();
        await SeedItemAsync(context, Guid.NewGuid(), branchId.Value, productId.Value);

        var repository = new EfInventoryItemRepository(context);
        var item = await repository.GetByBranchAndProductAsync(branchId, productId, CancellationToken.None);
        item!.Increase(1m, UpdatedAtUtc);

        await repository.UpdateAsync(item, CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var reloaded = await context.Set<InventoryItemRecord>().SingleAsync(r => r.Id == item.Id.Value);
        Assert.Equal(UpdatedAtUtc, reloaded.UpdatedAtUtc);
    }

    [Fact]
    public async Task UpdateAsyncDoesNotSaveAutomatically()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var branchId = BranchId.New();
        var productId = ProductId.New();
        await SeedItemAsync(context, Guid.NewGuid(), branchId.Value, productId.Value, quantity: 10m);

        var repository = new EfInventoryItemRepository(context);
        var item = await repository.GetByBranchAndProductAsync(branchId, productId, CancellationToken.None);
        item!.Increase(5m, UpdatedAtUtc);

        await repository.UpdateAsync(item, CancellationToken.None);

        var trackedEntry = context.ChangeTracker.Entries<InventoryItemRecord>().Single(e => e.Entity.Id == item.Id.Value);
        Assert.Equal(EntityState.Modified, trackedEntry.State);

        context.ChangeTracker.Clear();

        var reloaded = await context.Set<InventoryItemRecord>().SingleAsync(r => r.Id == item.Id.Value);
        Assert.Equal(10m, reloaded.Quantity);
    }

    [Fact]
    public async Task UpdateAsyncDoesNotChangeIdBranchIdProductIdOrCreatedAtUtc()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var branchId = BranchId.New();
        var productId = ProductId.New();
        var seeded = await SeedItemAsync(context, Guid.NewGuid(), branchId.Value, productId.Value);

        var repository = new EfInventoryItemRepository(context);
        var item = await repository.GetByBranchAndProductAsync(branchId, productId, CancellationToken.None);
        item!.Increase(1m, UpdatedAtUtc);

        await repository.UpdateAsync(item, CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var reloaded = await context.Set<InventoryItemRecord>().SingleAsync(r => r.Id == seeded.Id);
        Assert.Equal(seeded.Id, reloaded.Id);
        Assert.Equal(seeded.BranchId, reloaded.BranchId);
        Assert.Equal(seeded.ProductId, reloaded.ProductId);
        Assert.Equal(seeded.CreatedAtUtc, reloaded.CreatedAtUtc);
    }

    [Fact]
    public async Task UpdateAsyncThrowsEntityNotFoundExceptionWhenRecordDoesNotExist()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var branchId = BranchId.New();
        var productId = ProductId.New();
        await SeedItemAsync(context, Guid.NewGuid(), branchId.Value, productId.Value);

        var repository = new EfInventoryItemRepository(context);
        var item = await repository.GetByBranchAndProductAsync(branchId, productId, CancellationToken.None);
        item!.Increase(1m, UpdatedAtUtc);

        context.ChangeTracker.Clear();

        await using var otherConnection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await otherConnection.OpenAsync();
        await using var otherContext = CreateContext(otherConnection);
        await otherContext.Database.EnsureCreatedAsync();
        var otherRepository = new EfInventoryItemRepository(otherContext);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => otherRepository.UpdateAsync(item, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAsyncRejectsNullInventoryItem()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var repository = new EfInventoryItemRepository(context);

        await Assert.ThrowsAsync<ArgumentNullException>(() => repository.UpdateAsync(null!, CancellationToken.None));
    }

    [Fact]
    public void ConstructorRejectsNullContext()
    {
        Assert.Throws<ArgumentNullException>(() => new EfInventoryItemRepository(null!));
    }
}
