using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Pos.Domain.Sales;
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

    // ---------- SaleRecord ----------

    [Fact]
    public void SaleRecordShouldMapToSalesTable()
    {
        var entityType = BuildModel().FindEntityType(typeof(SaleRecord))!;

        Assert.Equal("sales", entityType.GetTableName());
    }

    [Fact]
    public void SaleRecordShouldHaveIdAsPrimaryKey()
    {
        var entityType = BuildModel().FindEntityType(typeof(SaleRecord))!;
        var primaryKey = entityType.FindPrimaryKey()!;

        var keyPropertyName = Assert.Single(primaryKey.Properties).Name;
        Assert.Equal(nameof(SaleRecord.Id), keyPropertyName);
    }

    [Fact]
    public void SaleRecordShouldHaveExpectedColumnNames()
    {
        var entityType = BuildModel().FindEntityType(typeof(SaleRecord))!;

        Assert.Equal("id", entityType.FindProperty(nameof(SaleRecord.Id))!.GetColumnName());
        Assert.Equal("organization_id", entityType.FindProperty(nameof(SaleRecord.OrganizationId))!.GetColumnName());
        Assert.Equal("branch_id", entityType.FindProperty(nameof(SaleRecord.BranchId))!.GetColumnName());
        Assert.Equal(
            "register_session_id",
            entityType.FindProperty(nameof(SaleRecord.RegisterSessionId))!.GetColumnName());
        Assert.Equal(
            "created_by_user_id",
            entityType.FindProperty(nameof(SaleRecord.CreatedByUserId))!.GetColumnName());
        Assert.Equal("currency", entityType.FindProperty(nameof(SaleRecord.Currency))!.GetColumnName());
        Assert.Equal("sale_status", entityType.FindProperty(nameof(SaleRecord.Status))!.GetColumnName());
        Assert.Equal(
            "created_at_utc_ticks",
            entityType.FindProperty(nameof(SaleRecord.CreatedAtUtc))!.GetColumnName());
        Assert.Equal(
            "completed_at_utc_ticks",
            entityType.FindProperty(nameof(SaleRecord.CompletedAtUtc))!.GetColumnName());
    }

    [Fact]
    public void SaleRecordCurrencyShouldHaveMaxLengthThree()
    {
        var entityType = BuildModel().FindEntityType(typeof(SaleRecord))!;

        Assert.Equal(3, entityType.FindProperty(nameof(SaleRecord.Currency))!.GetMaxLength());
    }

    [Fact]
    public void SaleRecordStatusShouldBeConvertedToString()
    {
        var entityType = BuildModel().FindEntityType(typeof(SaleRecord))!;
        var status = entityType.FindProperty(nameof(SaleRecord.Status))!;

        Assert.Equal(typeof(string), status.GetValueConverter()!.ProviderClrType);
        Assert.Equal(32, status.GetMaxLength());
    }

    [Fact]
    public void SaleRecordDatesShouldBeConvertedToLong()
    {
        var entityType = BuildModel().FindEntityType(typeof(SaleRecord))!;

        var createdAt = entityType.FindProperty(nameof(SaleRecord.CreatedAtUtc))!;
        var completedAt = entityType.FindProperty(nameof(SaleRecord.CompletedAtUtc))!;

        Assert.Equal(typeof(long), createdAt.GetValueConverter()!.ProviderClrType);
        Assert.Equal(typeof(long), completedAt.GetValueConverter()!.ProviderClrType);
    }

    [Fact]
    public void SaleRecordCompletedAtUtcShouldBeNullable()
    {
        var entityType = BuildModel().FindEntityType(typeof(SaleRecord))!;

        Assert.True(entityType.FindProperty(nameof(SaleRecord.CompletedAtUtc))!.IsNullable);
        Assert.False(entityType.FindProperty(nameof(SaleRecord.CreatedAtUtc))!.IsNullable);
    }

    [Fact]
    public void SaleRecordShouldHaveExpectedIndexes()
    {
        var entityType = BuildModel().FindEntityType(typeof(SaleRecord))!;
        var indexPropertySets = entityType.GetIndexes()
            .Select(index => index.Properties.Select(p => p.Name).ToArray())
            .ToArray();

        Assert.Contains(indexPropertySets, set => set.SequenceEqual([nameof(SaleRecord.OrganizationId)]));
        Assert.Contains(indexPropertySets, set => set.SequenceEqual([nameof(SaleRecord.BranchId)]));
        Assert.Contains(indexPropertySets, set => set.SequenceEqual([nameof(SaleRecord.RegisterSessionId)]));
        Assert.Contains(indexPropertySets, set => set.SequenceEqual([nameof(SaleRecord.CreatedAtUtc)]));
        Assert.Contains(indexPropertySets, set => set.SequenceEqual([nameof(SaleRecord.Status)]));
    }

    [Fact]
    public void SaleRecordShouldHaveCascadeRelationshipToLines()
    {
        var entityType = BuildModel().FindEntityType(typeof(SaleLineRecord))!;
        var foreignKey = Assert.Single(entityType.GetForeignKeys());

        Assert.Equal(typeof(SaleRecord), foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
        Assert.Equal(nameof(SaleLineRecord.SaleId), Assert.Single(foreignKey.Properties).Name);
    }

    [Fact]
    public void SaleRecordShouldHaveCascadeRelationshipToPayments()
    {
        var entityType = BuildModel().FindEntityType(typeof(PaymentRecord))!;
        var foreignKey = Assert.Single(entityType.GetForeignKeys());

        Assert.Equal(typeof(SaleRecord), foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
        Assert.Equal(nameof(PaymentRecord.SaleId), Assert.Single(foreignKey.Properties).Name);
    }

    // ---------- SaleLineRecord ----------

    [Fact]
    public void SaleLineRecordShouldMapToSaleLinesTable()
    {
        var entityType = BuildModel().FindEntityType(typeof(SaleLineRecord))!;

        Assert.Equal("sale_lines", entityType.GetTableName());
    }

    [Fact]
    public void SaleLineRecordShouldHaveIdAsPrimaryKey()
    {
        var entityType = BuildModel().FindEntityType(typeof(SaleLineRecord))!;
        var primaryKey = entityType.FindPrimaryKey()!;

        var keyPropertyName = Assert.Single(primaryKey.Properties).Name;
        Assert.Equal(nameof(SaleLineRecord.Id), keyPropertyName);
    }

    [Fact]
    public void SaleLineRecordShouldHaveExpectedColumnNames()
    {
        var entityType = BuildModel().FindEntityType(typeof(SaleLineRecord))!;

        Assert.Equal("id", entityType.FindProperty(nameof(SaleLineRecord.Id))!.GetColumnName());
        Assert.Equal("sale_id", entityType.FindProperty(nameof(SaleLineRecord.SaleId))!.GetColumnName());
        Assert.Equal("product_id", entityType.FindProperty(nameof(SaleLineRecord.ProductId))!.GetColumnName());
        Assert.Equal("product_sku", entityType.FindProperty(nameof(SaleLineRecord.ProductSku))!.GetColumnName());
        Assert.Equal("product_name", entityType.FindProperty(nameof(SaleLineRecord.ProductName))!.GetColumnName());
        Assert.Equal("quantity", entityType.FindProperty(nameof(SaleLineRecord.Quantity))!.GetColumnName());
        Assert.Equal(
            "unit_price_amount",
            entityType.FindProperty(nameof(SaleLineRecord.UnitPriceAmount))!.GetColumnName());
        Assert.Equal("currency", entityType.FindProperty(nameof(SaleLineRecord.Currency))!.GetColumnName());
    }

    [Fact]
    public void SaleLineRecordShouldHaveExpectedLengthsAndPrecision()
    {
        var entityType = BuildModel().FindEntityType(typeof(SaleLineRecord))!;

        Assert.Equal(40, entityType.FindProperty(nameof(SaleLineRecord.ProductSku))!.GetMaxLength());
        Assert.Equal(160, entityType.FindProperty(nameof(SaleLineRecord.ProductName))!.GetMaxLength());
        Assert.Equal(3, entityType.FindProperty(nameof(SaleLineRecord.Currency))!.GetMaxLength());

        var quantity = entityType.FindProperty(nameof(SaleLineRecord.Quantity))!;
        Assert.Equal(18, quantity.GetPrecision());
        Assert.Equal(6, quantity.GetScale());

        var unitPriceAmount = entityType.FindProperty(nameof(SaleLineRecord.UnitPriceAmount))!;
        Assert.Equal(18, unitPriceAmount.GetPrecision());
        Assert.Equal(2, unitPriceAmount.GetScale());
    }

    [Fact]
    public void SaleLineRecordShouldHaveUniqueIndexOnSaleIdAndProductId()
    {
        var entityType = BuildModel().FindEntityType(typeof(SaleLineRecord))!;

        var uniqueIndex = entityType.GetIndexes().SingleOrDefault(index =>
            index.IsUnique &&
            index.Properties.Select(p => p.Name).SequenceEqual(
            [
                nameof(SaleLineRecord.SaleId),
                nameof(SaleLineRecord.ProductId),
            ]));

        Assert.NotNull(uniqueIndex);
    }

    [Fact]
    public void SaleLineRecordShouldHaveIndexesOnSaleIdAndProductId()
    {
        var entityType = BuildModel().FindEntityType(typeof(SaleLineRecord))!;
        var indexPropertySets = entityType.GetIndexes()
            .Where(index => !index.IsUnique)
            .Select(index => index.Properties.Select(p => p.Name).ToArray())
            .ToArray();

        Assert.Contains(indexPropertySets, set => set.SequenceEqual([nameof(SaleLineRecord.SaleId)]));
        Assert.Contains(indexPropertySets, set => set.SequenceEqual([nameof(SaleLineRecord.ProductId)]));
    }

    // ---------- PaymentRecord ----------

    [Fact]
    public void PaymentRecordShouldMapToPaymentsTable()
    {
        var entityType = BuildModel().FindEntityType(typeof(PaymentRecord))!;

        Assert.Equal("payments", entityType.GetTableName());
    }

    [Fact]
    public void PaymentRecordShouldHaveIdAsPrimaryKey()
    {
        var entityType = BuildModel().FindEntityType(typeof(PaymentRecord))!;
        var primaryKey = entityType.FindPrimaryKey()!;

        var keyPropertyName = Assert.Single(primaryKey.Properties).Name;
        Assert.Equal(nameof(PaymentRecord.Id), keyPropertyName);
    }

    [Fact]
    public void PaymentRecordShouldHaveExpectedColumnNames()
    {
        var entityType = BuildModel().FindEntityType(typeof(PaymentRecord))!;

        Assert.Equal("id", entityType.FindProperty(nameof(PaymentRecord.Id))!.GetColumnName());
        Assert.Equal("sale_id", entityType.FindProperty(nameof(PaymentRecord.SaleId))!.GetColumnName());
        Assert.Equal("payment_method", entityType.FindProperty(nameof(PaymentRecord.Method))!.GetColumnName());
        Assert.Equal("amount", entityType.FindProperty(nameof(PaymentRecord.Amount))!.GetColumnName());
        Assert.Equal("currency", entityType.FindProperty(nameof(PaymentRecord.Currency))!.GetColumnName());
        Assert.Equal(
            "paid_at_utc_ticks",
            entityType.FindProperty(nameof(PaymentRecord.PaidAtUtc))!.GetColumnName());
    }

    [Fact]
    public void PaymentRecordMethodShouldBeConvertedToString()
    {
        var entityType = BuildModel().FindEntityType(typeof(PaymentRecord))!;
        var method = entityType.FindProperty(nameof(PaymentRecord.Method))!;

        Assert.Equal(typeof(string), method.GetValueConverter()!.ProviderClrType);
        Assert.Equal(32, method.GetMaxLength());
    }

    [Fact]
    public void PaymentRecordShouldHaveExpectedLengthsAndPrecision()
    {
        var entityType = BuildModel().FindEntityType(typeof(PaymentRecord))!;

        Assert.Equal(3, entityType.FindProperty(nameof(PaymentRecord.Currency))!.GetMaxLength());

        var amount = entityType.FindProperty(nameof(PaymentRecord.Amount))!;
        Assert.Equal(18, amount.GetPrecision());
        Assert.Equal(2, amount.GetScale());
    }

    [Fact]
    public void PaymentRecordPaidAtUtcShouldBeConvertedToLong()
    {
        var entityType = BuildModel().FindEntityType(typeof(PaymentRecord))!;
        var paidAt = entityType.FindProperty(nameof(PaymentRecord.PaidAtUtc))!;

        Assert.Equal(typeof(long), paidAt.GetValueConverter()!.ProviderClrType);
    }

    [Fact]
    public void PaymentRecordShouldHaveExpectedIndexes()
    {
        var entityType = BuildModel().FindEntityType(typeof(PaymentRecord))!;
        var indexPropertySets = entityType.GetIndexes()
            .Select(index => index.Properties.Select(p => p.Name).ToArray())
            .ToArray();

        Assert.Contains(indexPropertySets, set => set.SequenceEqual([nameof(PaymentRecord.SaleId)]));
        Assert.Contains(indexPropertySets, set => set.SequenceEqual([nameof(PaymentRecord.Method)]));
        Assert.Contains(indexPropertySets, set => set.SequenceEqual([nameof(PaymentRecord.PaidAtUtc)]));
    }
}
