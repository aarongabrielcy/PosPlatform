using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Inventory;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Persistence.Records;
using Pos.Infrastructure.Persistence.Repositories;

namespace Pos.Infrastructure.Tests.Persistence.Repositories;

public class EfInventoryMovementRepositoryTests
{
    private static readonly DateTimeOffset OccurredAtUtc = new(2026, 1, 1, 9, 30, 0, TimeSpan.Zero);

    private static PosDbContext CreateContext(SqliteConnection connection)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PosDbContext>();
        optionsBuilder.UseSqlite(connection);

        return new PosDbContext(optionsBuilder.Options);
    }

    private static async Task SeedInventoryItemAsync(PosDbContext context, Guid id, Guid branchId, Guid productId)
    {
        context.Add(new InventoryItemRecord
        {
            Id = id,
            BranchId = branchId,
            ProductId = productId,
            Quantity = 10m,
            ReorderPoint = 2m,
            CreatedAtUtc = OccurredAtUtc,
            UpdatedAtUtc = OccurredAtUtc,
        });

        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();
    }

    private static InventoryMovement CreateManualDecrease(InventoryItemId inventoryItemId, BranchId branchId, ProductId productId) =>
        InventoryMovement.CreateManualDecrease(
            InventoryMovementId.New(),
            inventoryItemId,
            branchId,
            productId,
            UserId.New(),
            quantity: 4m,
            quantityBefore: 10m,
            OccurredAtUtc);

    private static InventoryMovement CreateSaleDecrease(InventoryItemId inventoryItemId, BranchId branchId, ProductId productId) =>
        InventoryMovement.CreateSaleDecrease(
            InventoryMovementId.New(),
            inventoryItemId,
            branchId,
            productId,
            UserId.New(),
            SaleId.New(),
            SaleLineId.New(),
            quantity: 3m,
            quantityBefore: 10m,
            OccurredAtUtc);

    [Fact]
    public async Task AddAsyncTracksRecordAsAdded()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var itemId = InventoryItemId.New();
        var branchId = BranchId.New();
        var productId = ProductId.New();
        await SeedInventoryItemAsync(context, itemId.Value, branchId.Value, productId.Value);

        var repository = new EfInventoryMovementRepository(context);
        var movement = CreateManualDecrease(itemId, branchId, productId);

        await repository.AddAsync(movement, CancellationToken.None);

        var entry = context.ChangeTracker.Entries<InventoryMovementRecord>().Single(e => e.Entity.Id == movement.Id.Value);
        Assert.Equal(EntityState.Added, entry.State);
    }

    [Fact]
    public async Task AddAsyncDoesNotPersistBeforeCommit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var itemId = InventoryItemId.New();
        var branchId = BranchId.New();
        var productId = ProductId.New();
        await SeedInventoryItemAsync(context, itemId.Value, branchId.Value, productId.Value);

        var repository = new EfInventoryMovementRepository(context);
        var movement = CreateManualDecrease(itemId, branchId, productId);

        await repository.AddAsync(movement, CancellationToken.None);
        context.ChangeTracker.Clear();

        var exists = await context.Set<InventoryMovementRecord>().AnyAsync(r => r.Id == movement.Id.Value);
        Assert.False(exists);
    }

    [Fact]
    public async Task AddAsyncPersistsAfterCommit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var itemId = InventoryItemId.New();
        var branchId = BranchId.New();
        var productId = ProductId.New();
        await SeedInventoryItemAsync(context, itemId.Value, branchId.Value, productId.Value);

        var repository = new EfInventoryMovementRepository(context);
        var movement = CreateManualDecrease(itemId, branchId, productId);

        await repository.AddAsync(movement, CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var reloaded = await context.Set<InventoryMovementRecord>().SingleAsync(r => r.Id == movement.Id.Value);
        Assert.Equal(movement.Id.Value, reloaded.Id);
        Assert.Equal(movement.Quantity, reloaded.Quantity);
        Assert.Equal(movement.QuantityBefore, reloaded.QuantityBefore);
        Assert.Equal(movement.QuantityAfter, reloaded.QuantityAfter);
    }

    [Fact]
    public async Task AddAsyncPreservesAllFields()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var itemId = InventoryItemId.New();
        var branchId = BranchId.New();
        var productId = ProductId.New();
        await SeedInventoryItemAsync(context, itemId.Value, branchId.Value, productId.Value);

        var repository = new EfInventoryMovementRepository(context);
        var movement = CreateSaleDecrease(itemId, branchId, productId);

        await repository.AddAsync(movement, CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var reloaded = await context.Set<InventoryMovementRecord>().SingleAsync(r => r.Id == movement.Id.Value);
        Assert.Equal(movement.InventoryItemId.Value, reloaded.InventoryItemId);
        Assert.Equal(movement.BranchId.Value, reloaded.BranchId);
        Assert.Equal(movement.ProductId.Value, reloaded.ProductId);
        Assert.Equal(movement.PerformedByUserId.Value, reloaded.PerformedByUserId);
        Assert.Equal(movement.Type, reloaded.Type);
        Assert.Equal(movement.OccurredAtUtc, reloaded.OccurredAtUtc);
    }

    [Fact]
    public async Task AddAsyncPreservesSaleDecreaseSaleReferences()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var itemId = InventoryItemId.New();
        var branchId = BranchId.New();
        var productId = ProductId.New();
        await SeedInventoryItemAsync(context, itemId.Value, branchId.Value, productId.Value);

        var repository = new EfInventoryMovementRepository(context);
        var movement = CreateSaleDecrease(itemId, branchId, productId);

        await repository.AddAsync(movement, CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var reloaded = await context.Set<InventoryMovementRecord>().SingleAsync(r => r.Id == movement.Id.Value);
        Assert.Equal(movement.SaleId!.Value.Value, reloaded.SaleId);
        Assert.Equal(movement.SaleLineId!.Value.Value, reloaded.SaleLineId);
    }

    [Fact]
    public async Task AddAsyncKeepsManualDecreaseSaleReferencesNull()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var itemId = InventoryItemId.New();
        var branchId = BranchId.New();
        var productId = ProductId.New();
        await SeedInventoryItemAsync(context, itemId.Value, branchId.Value, productId.Value);

        var repository = new EfInventoryMovementRepository(context);
        var movement = CreateManualDecrease(itemId, branchId, productId);

        await repository.AddAsync(movement, CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var reloaded = await context.Set<InventoryMovementRecord>().SingleAsync(r => r.Id == movement.Id.Value);
        Assert.Null(reloaded.SaleId);
        Assert.Null(reloaded.SaleLineId);
    }

    [Fact]
    public async Task AddAsyncRejectsNullMovement()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var repository = new EfInventoryMovementRepository(context);

        await Assert.ThrowsAsync<ArgumentNullException>(() => repository.AddAsync(null!, CancellationToken.None));
    }

    [Fact]
    public void ConstructorRejectsNullContext()
    {
        Assert.Throws<ArgumentNullException>(() => new EfInventoryMovementRepository(null!));
    }
}
