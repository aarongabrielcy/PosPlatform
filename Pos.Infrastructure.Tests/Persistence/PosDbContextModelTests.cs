using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Tests.Persistence;

public class PosDbContextModelTests
{
    private static IModel BuildModel()
    {
        var optionsBuilder = new DbContextOptionsBuilder<PosDbContext>();
        optionsBuilder.UseSqlite("Data Source=:memory:");

        using var context = new PosDbContext(optionsBuilder.Options);
        return context.Model;
    }

    [Fact]
    public void InventoryItemRecordShouldMapToInventoryItemsTable()
    {
        var entityType = BuildModel().FindEntityType(typeof(InventoryItemRecord))!;

        Assert.Equal("inventory_items", entityType.GetTableName());
    }

    [Fact]
    public void InventoryItemRecordShouldHaveIdAsPrimaryKey()
    {
        var entityType = BuildModel().FindEntityType(typeof(InventoryItemRecord))!;
        var primaryKey = entityType.FindPrimaryKey()!;

        var keyPropertyName = Assert.Single(primaryKey.Properties).Name;
        Assert.Equal(nameof(InventoryItemRecord.Id), keyPropertyName);
        Assert.Equal("id", entityType.FindProperty(nameof(InventoryItemRecord.Id))!.GetColumnName());
    }

    [Fact]
    public void InventoryItemRecordShouldHaveExpectedColumnNames()
    {
        var entityType = BuildModel().FindEntityType(typeof(InventoryItemRecord))!;

        Assert.Equal("id", entityType.FindProperty(nameof(InventoryItemRecord.Id))!.GetColumnName());
        Assert.Equal("branch_id", entityType.FindProperty(nameof(InventoryItemRecord.BranchId))!.GetColumnName());
        Assert.Equal("product_id", entityType.FindProperty(nameof(InventoryItemRecord.ProductId))!.GetColumnName());
        Assert.Equal("quantity", entityType.FindProperty(nameof(InventoryItemRecord.Quantity))!.GetColumnName());
        Assert.Equal("reorder_point", entityType.FindProperty(nameof(InventoryItemRecord.ReorderPoint))!.GetColumnName());
        Assert.Equal(
            "created_at_utc_ticks",
            entityType.FindProperty(nameof(InventoryItemRecord.CreatedAtUtc))!.GetColumnName());
        Assert.Equal(
            "updated_at_utc_ticks",
            entityType.FindProperty(nameof(InventoryItemRecord.UpdatedAtUtc))!.GetColumnName());
    }

    [Fact]
    public void InventoryItemRecordShouldHaveUniqueIndexOnBranchIdAndProductId()
    {
        var entityType = BuildModel().FindEntityType(typeof(InventoryItemRecord))!;

        var uniqueIndex = entityType.GetIndexes().SingleOrDefault(index =>
            index.IsUnique &&
            index.Properties.Select(p => p.Name).SequenceEqual(
            [
                nameof(InventoryItemRecord.BranchId),
                nameof(InventoryItemRecord.ProductId),
            ]));

        Assert.NotNull(uniqueIndex);
    }

    [Fact]
    public void InventoryItemRecordShouldHaveIndexOnProductId()
    {
        var entityType = BuildModel().FindEntityType(typeof(InventoryItemRecord))!;

        var index = entityType.GetIndexes().SingleOrDefault(index =>
            !index.IsUnique &&
            index.Properties.Select(p => p.Name).SequenceEqual([nameof(InventoryItemRecord.ProductId)]));

        Assert.NotNull(index);
    }

    [Fact]
    public void InventoryItemRecordDatesShouldBeConvertedToLong()
    {
        var entityType = BuildModel().FindEntityType(typeof(InventoryItemRecord))!;

        var createdAt = entityType.FindProperty(nameof(InventoryItemRecord.CreatedAtUtc))!;
        var updatedAt = entityType.FindProperty(nameof(InventoryItemRecord.UpdatedAtUtc))!;

        Assert.Equal(typeof(long), createdAt.GetValueConverter()!.ProviderClrType);
        Assert.Equal(typeof(long), updatedAt.GetValueConverter()!.ProviderClrType);
    }

    [Fact]
    public void InventoryItemRecordQuantitiesShouldRemainDecimal()
    {
        var entityType = BuildModel().FindEntityType(typeof(InventoryItemRecord))!;

        var quantity = entityType.FindProperty(nameof(InventoryItemRecord.Quantity))!;
        var reorderPoint = entityType.FindProperty(nameof(InventoryItemRecord.ReorderPoint))!;

        Assert.Equal(typeof(decimal), quantity.ClrType);
        Assert.Equal(typeof(decimal), reorderPoint.ClrType);
    }

    [Fact]
    public void InventoryMovementRecordShouldMapToInventoryMovementsTable()
    {
        var entityType = BuildModel().FindEntityType(typeof(InventoryMovementRecord))!;

        Assert.Equal("inventory_movements", entityType.GetTableName());
    }

    [Fact]
    public void InventoryMovementRecordShouldHaveIdAsPrimaryKey()
    {
        var entityType = BuildModel().FindEntityType(typeof(InventoryMovementRecord))!;
        var primaryKey = entityType.FindPrimaryKey()!;

        var keyPropertyName = Assert.Single(primaryKey.Properties).Name;
        Assert.Equal(nameof(InventoryMovementRecord.Id), keyPropertyName);
    }

    [Fact]
    public void InventoryMovementRecordTypeShouldBeConvertedToString()
    {
        var entityType = BuildModel().FindEntityType(typeof(InventoryMovementRecord))!;
        var type = entityType.FindProperty(nameof(InventoryMovementRecord.Type))!;

        Assert.Equal(typeof(string), type.GetValueConverter()!.ProviderClrType);
    }

    [Fact]
    public void InventoryMovementRecordShouldHaveRestrictForeignKeyToInventoryItems()
    {
        var entityType = BuildModel().FindEntityType(typeof(InventoryMovementRecord))!;
        var foreignKey = Assert.Single(entityType.GetForeignKeys());

        Assert.Equal(typeof(InventoryItemRecord), foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
        Assert.Equal(
            nameof(InventoryMovementRecord.InventoryItemId),
            Assert.Single(foreignKey.Properties).Name);
    }

    [Fact]
    public void InventoryMovementRecordShouldHaveExpectedIndexes()
    {
        var entityType = BuildModel().FindEntityType(typeof(InventoryMovementRecord))!;
        var indexPropertySets = entityType.GetIndexes()
            .Select(index => index.Properties.Select(p => p.Name).ToArray())
            .ToArray();

        Assert.Contains(indexPropertySets, set => set.SequenceEqual([nameof(InventoryMovementRecord.InventoryItemId)]));
        Assert.Contains(
            indexPropertySets,
            set => set.SequenceEqual(
            [
                nameof(InventoryMovementRecord.BranchId),
                nameof(InventoryMovementRecord.ProductId),
            ]));
        Assert.Contains(indexPropertySets, set => set.SequenceEqual([nameof(InventoryMovementRecord.OccurredAtUtc)]));
        Assert.Contains(indexPropertySets, set => set.SequenceEqual([nameof(InventoryMovementRecord.SaleId)]));
        Assert.Contains(indexPropertySets, set => set.SequenceEqual([nameof(InventoryMovementRecord.SaleLineId)]));
    }

    [Fact]
    public void InventoryMovementRecordOccurredAtShouldBeConvertedToLong()
    {
        var entityType = BuildModel().FindEntityType(typeof(InventoryMovementRecord))!;
        var occurredAt = entityType.FindProperty(nameof(InventoryMovementRecord.OccurredAtUtc))!;

        Assert.Equal(typeof(long), occurredAt.GetValueConverter()!.ProviderClrType);
    }

    [Fact]
    public void InventoryMovementRecordSaleReferencesShouldBeOptional()
    {
        var entityType = BuildModel().FindEntityType(typeof(InventoryMovementRecord))!;

        Assert.True(entityType.FindProperty(nameof(InventoryMovementRecord.SaleId))!.IsNullable);
        Assert.True(entityType.FindProperty(nameof(InventoryMovementRecord.SaleLineId))!.IsNullable);
    }
}
