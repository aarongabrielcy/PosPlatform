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

    // ---------- OrganizationRecord ----------

    [Fact]
    public void OrganizationRecordShouldMapToOrganizationsTable()
    {
        var entityType = BuildModel().FindEntityType(typeof(OrganizationRecord))!;

        Assert.Equal("organizations", entityType.GetTableName());
    }

    [Fact]
    public void OrganizationRecordShouldHaveIdAsPrimaryKey()
    {
        var entityType = BuildModel().FindEntityType(typeof(OrganizationRecord))!;
        var primaryKey = entityType.FindPrimaryKey()!;

        var keyPropertyName = Assert.Single(primaryKey.Properties).Name;
        Assert.Equal(nameof(OrganizationRecord.Id), keyPropertyName);
    }

    [Fact]
    public void OrganizationRecordShouldHaveExpectedColumnNames()
    {
        var entityType = BuildModel().FindEntityType(typeof(OrganizationRecord))!;

        Assert.Equal("id", entityType.FindProperty(nameof(OrganizationRecord.Id))!.GetColumnName());
        Assert.Equal("name", entityType.FindProperty(nameof(OrganizationRecord.Name))!.GetColumnName());
        Assert.Equal("is_active", entityType.FindProperty(nameof(OrganizationRecord.IsActive))!.GetColumnName());
        Assert.Equal(
            "created_at_utc_ticks",
            entityType.FindProperty(nameof(OrganizationRecord.CreatedAtUtc))!.GetColumnName());
    }

    [Fact]
    public void OrganizationRecordNameShouldHaveMaxLength120()
    {
        var entityType = BuildModel().FindEntityType(typeof(OrganizationRecord))!;

        Assert.Equal(120, entityType.FindProperty(nameof(OrganizationRecord.Name))!.GetMaxLength());
    }

    [Fact]
    public void OrganizationRecordCreatedAtShouldBeConvertedToLong()
    {
        var entityType = BuildModel().FindEntityType(typeof(OrganizationRecord))!;
        var createdAt = entityType.FindProperty(nameof(OrganizationRecord.CreatedAtUtc))!;

        Assert.Equal(typeof(long), createdAt.GetValueConverter()!.ProviderClrType);
    }

    [Fact]
    public void OrganizationRecordShouldHaveExpectedIndexes()
    {
        var entityType = BuildModel().FindEntityType(typeof(OrganizationRecord))!;
        var indexPropertySets = entityType.GetIndexes()
            .Select(index => index.Properties.Select(p => p.Name).ToArray())
            .ToArray();

        Assert.Contains(indexPropertySets, set => set.SequenceEqual([nameof(OrganizationRecord.IsActive)]));
        Assert.Contains(indexPropertySets, set => set.SequenceEqual([nameof(OrganizationRecord.Name)]));
    }

    [Fact]
    public void OrganizationRecordShouldNotHaveUniqueIndexOnName()
    {
        var entityType = BuildModel().FindEntityType(typeof(OrganizationRecord))!;

        var uniqueIndex = entityType.GetIndexes().SingleOrDefault(index =>
            index.IsUnique &&
            index.Properties.Select(p => p.Name).SequenceEqual([nameof(OrganizationRecord.Name)]));

        Assert.Null(uniqueIndex);
    }

    // ---------- BranchRecord ----------

    [Fact]
    public void BranchRecordShouldMapToBranchesTable()
    {
        var entityType = BuildModel().FindEntityType(typeof(BranchRecord))!;

        Assert.Equal("branches", entityType.GetTableName());
    }

    [Fact]
    public void BranchRecordShouldHaveIdAsPrimaryKey()
    {
        var entityType = BuildModel().FindEntityType(typeof(BranchRecord))!;
        var primaryKey = entityType.FindPrimaryKey()!;

        var keyPropertyName = Assert.Single(primaryKey.Properties).Name;
        Assert.Equal(nameof(BranchRecord.Id), keyPropertyName);
    }

    [Fact]
    public void BranchRecordShouldHaveExpectedColumnNames()
    {
        var entityType = BuildModel().FindEntityType(typeof(BranchRecord))!;

        Assert.Equal("id", entityType.FindProperty(nameof(BranchRecord.Id))!.GetColumnName());
        Assert.Equal(
            "organization_id",
            entityType.FindProperty(nameof(BranchRecord.OrganizationId))!.GetColumnName());
        Assert.Equal("name", entityType.FindProperty(nameof(BranchRecord.Name))!.GetColumnName());
        Assert.Equal("code", entityType.FindProperty(nameof(BranchRecord.Code))!.GetColumnName());
        Assert.Equal("is_active", entityType.FindProperty(nameof(BranchRecord.IsActive))!.GetColumnName());
        Assert.Equal(
            "created_at_utc_ticks",
            entityType.FindProperty(nameof(BranchRecord.CreatedAtUtc))!.GetColumnName());
    }

    [Fact]
    public void BranchRecordShouldHaveExpectedLengths()
    {
        var entityType = BuildModel().FindEntityType(typeof(BranchRecord))!;

        Assert.Equal(120, entityType.FindProperty(nameof(BranchRecord.Name))!.GetMaxLength());
        Assert.Equal(20, entityType.FindProperty(nameof(BranchRecord.Code))!.GetMaxLength());
    }

    [Fact]
    public void BranchRecordCreatedAtShouldBeConvertedToLong()
    {
        var entityType = BuildModel().FindEntityType(typeof(BranchRecord))!;
        var createdAt = entityType.FindProperty(nameof(BranchRecord.CreatedAtUtc))!;

        Assert.Equal(typeof(long), createdAt.GetValueConverter()!.ProviderClrType);
    }

    [Fact]
    public void BranchRecordShouldHaveRestrictForeignKeyToOrganizations()
    {
        var entityType = BuildModel().FindEntityType(typeof(BranchRecord))!;
        var foreignKey = Assert.Single(entityType.GetForeignKeys());

        Assert.Equal(typeof(OrganizationRecord), foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
        Assert.Equal(nameof(BranchRecord.OrganizationId), Assert.Single(foreignKey.Properties).Name);
    }

    [Fact]
    public void BranchRecordShouldHaveNonUniqueIndexOnOrganizationIdAndName()
    {
        var entityType = BuildModel().FindEntityType(typeof(BranchRecord))!;

        var index = entityType.GetIndexes().SingleOrDefault(index =>
            !index.IsUnique &&
            index.Properties.Select(p => p.Name).SequenceEqual(
            [
                nameof(BranchRecord.OrganizationId),
                nameof(BranchRecord.Name),
            ]));

        Assert.NotNull(index);
    }

    [Fact]
    public void BranchRecordShouldNotHaveUniqueIndexOnOrganizationIdAndName()
    {
        var entityType = BuildModel().FindEntityType(typeof(BranchRecord))!;

        var uniqueIndex = entityType.GetIndexes().SingleOrDefault(index =>
            index.IsUnique &&
            index.Properties.Select(p => p.Name).SequenceEqual(
            [
                nameof(BranchRecord.OrganizationId),
                nameof(BranchRecord.Name),
            ]));

        Assert.Null(uniqueIndex);
    }

    [Fact]
    public void BranchRecordShouldHaveExpectedIndexes()
    {
        var entityType = BuildModel().FindEntityType(typeof(BranchRecord))!;
        var indexPropertySets = entityType.GetIndexes()
            .Where(index => !index.IsUnique)
            .Select(index => index.Properties.Select(p => p.Name).ToArray())
            .ToArray();

        Assert.Contains(
            indexPropertySets,
            set => set.SequenceEqual([nameof(BranchRecord.OrganizationId), nameof(BranchRecord.Name)]));
        Assert.Contains(indexPropertySets, set => set.SequenceEqual([nameof(BranchRecord.IsActive)]));
    }

    // ---------- RegisterRecord ----------

    [Fact]
    public void RegisterRecordShouldMapToRegistersTable()
    {
        var entityType = BuildModel().FindEntityType(typeof(RegisterRecord))!;

        Assert.Equal("registers", entityType.GetTableName());
    }

    [Fact]
    public void RegisterRecordShouldHaveIdAsPrimaryKey()
    {
        var entityType = BuildModel().FindEntityType(typeof(RegisterRecord))!;
        var primaryKey = entityType.FindPrimaryKey()!;

        var keyPropertyName = Assert.Single(primaryKey.Properties).Name;
        Assert.Equal(nameof(RegisterRecord.Id), keyPropertyName);
    }

    [Fact]
    public void RegisterRecordShouldHaveExpectedColumnNames()
    {
        var entityType = BuildModel().FindEntityType(typeof(RegisterRecord))!;

        Assert.Equal("id", entityType.FindProperty(nameof(RegisterRecord.Id))!.GetColumnName());
        Assert.Equal("branch_id", entityType.FindProperty(nameof(RegisterRecord.BranchId))!.GetColumnName());
        Assert.Equal("name", entityType.FindProperty(nameof(RegisterRecord.Name))!.GetColumnName());
        Assert.Equal("code", entityType.FindProperty(nameof(RegisterRecord.Code))!.GetColumnName());
        Assert.Equal("is_active", entityType.FindProperty(nameof(RegisterRecord.IsActive))!.GetColumnName());
        Assert.Equal(
            "created_at_utc_ticks",
            entityType.FindProperty(nameof(RegisterRecord.CreatedAtUtc))!.GetColumnName());
    }

    [Fact]
    public void RegisterRecordShouldHaveExpectedLengths()
    {
        var entityType = BuildModel().FindEntityType(typeof(RegisterRecord))!;

        Assert.Equal(80, entityType.FindProperty(nameof(RegisterRecord.Name))!.GetMaxLength());
        Assert.Equal(20, entityType.FindProperty(nameof(RegisterRecord.Code))!.GetMaxLength());
    }

    [Fact]
    public void RegisterRecordCreatedAtShouldBeConvertedToLong()
    {
        var entityType = BuildModel().FindEntityType(typeof(RegisterRecord))!;
        var createdAt = entityType.FindProperty(nameof(RegisterRecord.CreatedAtUtc))!;

        Assert.Equal(typeof(long), createdAt.GetValueConverter()!.ProviderClrType);
    }

    [Fact]
    public void RegisterRecordShouldHaveRestrictForeignKeyToBranches()
    {
        var entityType = BuildModel().FindEntityType(typeof(RegisterRecord))!;
        var foreignKey = Assert.Single(entityType.GetForeignKeys());

        Assert.Equal(typeof(BranchRecord), foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
        Assert.Equal(nameof(RegisterRecord.BranchId), Assert.Single(foreignKey.Properties).Name);
    }

    [Fact]
    public void RegisterRecordShouldHaveNonUniqueIndexOnBranchIdAndName()
    {
        var entityType = BuildModel().FindEntityType(typeof(RegisterRecord))!;

        var index = entityType.GetIndexes().SingleOrDefault(index =>
            !index.IsUnique &&
            index.Properties.Select(p => p.Name).SequenceEqual(
            [
                nameof(RegisterRecord.BranchId),
                nameof(RegisterRecord.Name),
            ]));

        Assert.NotNull(index);
    }

    [Fact]
    public void RegisterRecordShouldNotHaveUniqueIndexOnBranchIdAndName()
    {
        var entityType = BuildModel().FindEntityType(typeof(RegisterRecord))!;

        var uniqueIndex = entityType.GetIndexes().SingleOrDefault(index =>
            index.IsUnique &&
            index.Properties.Select(p => p.Name).SequenceEqual(
            [
                nameof(RegisterRecord.BranchId),
                nameof(RegisterRecord.Name),
            ]));

        Assert.Null(uniqueIndex);
    }

    [Fact]
    public void RegisterRecordShouldHaveExpectedIndexes()
    {
        var entityType = BuildModel().FindEntityType(typeof(RegisterRecord))!;
        var indexPropertySets = entityType.GetIndexes()
            .Where(index => !index.IsUnique)
            .Select(index => index.Properties.Select(p => p.Name).ToArray())
            .ToArray();

        Assert.Contains(
            indexPropertySets,
            set => set.SequenceEqual([nameof(RegisterRecord.BranchId), nameof(RegisterRecord.Name)]));
        Assert.Contains(indexPropertySets, set => set.SequenceEqual([nameof(RegisterRecord.IsActive)]));
    }

    // ---------- ProductRecord ----------

    [Fact]
    public void ProductRecordShouldMapToProductsTable()
    {
        var entityType = BuildModel().FindEntityType(typeof(ProductRecord))!;

        Assert.Equal("products", entityType.GetTableName());
    }

    [Fact]
    public void ProductRecordShouldHaveIdAsPrimaryKey()
    {
        var entityType = BuildModel().FindEntityType(typeof(ProductRecord))!;
        var primaryKey = entityType.FindPrimaryKey()!;

        var keyPropertyName = Assert.Single(primaryKey.Properties).Name;
        Assert.Equal(nameof(ProductRecord.Id), keyPropertyName);
    }

    [Fact]
    public void ProductRecordShouldHaveExpectedColumnNames()
    {
        var entityType = BuildModel().FindEntityType(typeof(ProductRecord))!;

        Assert.Equal("id", entityType.FindProperty(nameof(ProductRecord.Id))!.GetColumnName());
        Assert.Equal(
            "organization_id",
            entityType.FindProperty(nameof(ProductRecord.OrganizationId))!.GetColumnName());
        Assert.Equal("sku", entityType.FindProperty(nameof(ProductRecord.Sku))!.GetColumnName());
        Assert.Equal("barcode", entityType.FindProperty(nameof(ProductRecord.Barcode))!.GetColumnName());
        Assert.Equal("name", entityType.FindProperty(nameof(ProductRecord.Name))!.GetColumnName());
        Assert.Equal("description", entityType.FindProperty(nameof(ProductRecord.Description))!.GetColumnName());
        Assert.Equal(
            "sale_price_amount",
            entityType.FindProperty(nameof(ProductRecord.SalePriceAmount))!.GetColumnName());
        Assert.Equal(
            "sale_price_currency",
            entityType.FindProperty(nameof(ProductRecord.SalePriceCurrency))!.GetColumnName());
        Assert.Equal("cost_amount", entityType.FindProperty(nameof(ProductRecord.CostAmount))!.GetColumnName());
        Assert.Equal("cost_currency", entityType.FindProperty(nameof(ProductRecord.CostCurrency))!.GetColumnName());
        Assert.Equal(
            "tracks_inventory",
            entityType.FindProperty(nameof(ProductRecord.TracksInventory))!.GetColumnName());
        Assert.Equal("is_active", entityType.FindProperty(nameof(ProductRecord.IsActive))!.GetColumnName());
        Assert.Equal(
            "created_at_utc_ticks",
            entityType.FindProperty(nameof(ProductRecord.CreatedAtUtc))!.GetColumnName());
    }

    [Fact]
    public void ProductRecordShouldHaveExpectedLengths()
    {
        var entityType = BuildModel().FindEntityType(typeof(ProductRecord))!;

        Assert.Equal(40, entityType.FindProperty(nameof(ProductRecord.Sku))!.GetMaxLength());
        Assert.Equal(32, entityType.FindProperty(nameof(ProductRecord.Barcode))!.GetMaxLength());
        Assert.Equal(160, entityType.FindProperty(nameof(ProductRecord.Name))!.GetMaxLength());
        Assert.Equal(500, entityType.FindProperty(nameof(ProductRecord.Description))!.GetMaxLength());
        Assert.Equal(3, entityType.FindProperty(nameof(ProductRecord.SalePriceCurrency))!.GetMaxLength());
        Assert.Equal(3, entityType.FindProperty(nameof(ProductRecord.CostCurrency))!.GetMaxLength());
    }

    [Fact]
    public void ProductRecordShouldHaveExpectedPrecision()
    {
        var entityType = BuildModel().FindEntityType(typeof(ProductRecord))!;

        var salePrice = entityType.FindProperty(nameof(ProductRecord.SalePriceAmount))!;
        Assert.Equal(18, salePrice.GetPrecision());
        Assert.Equal(2, salePrice.GetScale());

        var cost = entityType.FindProperty(nameof(ProductRecord.CostAmount))!;
        Assert.Equal(18, cost.GetPrecision());
        Assert.Equal(2, cost.GetScale());
    }

    [Fact]
    public void ProductRecordAmountsShouldRemainDecimal()
    {
        var entityType = BuildModel().FindEntityType(typeof(ProductRecord))!;

        Assert.Equal(typeof(decimal), entityType.FindProperty(nameof(ProductRecord.SalePriceAmount))!.ClrType);
        Assert.Equal(typeof(decimal?), entityType.FindProperty(nameof(ProductRecord.CostAmount))!.ClrType);
    }

    [Fact]
    public void ProductRecordBarcodeAndCostShouldBeNullable()
    {
        var entityType = BuildModel().FindEntityType(typeof(ProductRecord))!;

        Assert.True(entityType.FindProperty(nameof(ProductRecord.Barcode))!.IsNullable);
        Assert.True(entityType.FindProperty(nameof(ProductRecord.CostAmount))!.IsNullable);
        Assert.True(entityType.FindProperty(nameof(ProductRecord.CostCurrency))!.IsNullable);
        Assert.True(entityType.FindProperty(nameof(ProductRecord.Description))!.IsNullable);
        Assert.False(entityType.FindProperty(nameof(ProductRecord.Sku))!.IsNullable);
        Assert.False(entityType.FindProperty(nameof(ProductRecord.SalePriceAmount))!.IsNullable);
    }

    [Fact]
    public void ProductRecordCreatedAtShouldBeConvertedToLong()
    {
        var entityType = BuildModel().FindEntityType(typeof(ProductRecord))!;
        var createdAt = entityType.FindProperty(nameof(ProductRecord.CreatedAtUtc))!;

        Assert.Equal(typeof(long), createdAt.GetValueConverter()!.ProviderClrType);
    }

    [Fact]
    public void ProductRecordShouldHaveRestrictForeignKeyToOrganizations()
    {
        var entityType = BuildModel().FindEntityType(typeof(ProductRecord))!;
        var foreignKey = Assert.Single(entityType.GetForeignKeys());

        Assert.Equal(typeof(OrganizationRecord), foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
        Assert.Equal(nameof(ProductRecord.OrganizationId), Assert.Single(foreignKey.Properties).Name);
    }

    [Fact]
    public void ProductRecordShouldHaveUniqueIndexOnOrganizationIdAndSku()
    {
        var entityType = BuildModel().FindEntityType(typeof(ProductRecord))!;

        var uniqueIndex = entityType.GetIndexes().SingleOrDefault(index =>
            index.IsUnique &&
            index.Properties.Select(p => p.Name).SequenceEqual(
            [
                nameof(ProductRecord.OrganizationId),
                nameof(ProductRecord.Sku),
            ]));

        Assert.NotNull(uniqueIndex);
    }

    [Fact]
    public void ProductRecordShouldHaveNonUniqueIndexOnOrganizationIdAndBarcode()
    {
        var entityType = BuildModel().FindEntityType(typeof(ProductRecord))!;

        var index = entityType.GetIndexes().SingleOrDefault(index =>
            !index.IsUnique &&
            index.Properties.Select(p => p.Name).SequenceEqual(
            [
                nameof(ProductRecord.OrganizationId),
                nameof(ProductRecord.Barcode),
            ]));

        Assert.NotNull(index);
    }

    [Fact]
    public void ProductRecordShouldNotHaveAnyUniqueIndexContainingOnlyBarcode()
    {
        var entityType = BuildModel().FindEntityType(typeof(ProductRecord))!;

        var uniqueIndex = entityType.GetIndexes().SingleOrDefault(index =>
            index.IsUnique &&
            index.Properties.Select(p => p.Name).SequenceEqual([nameof(ProductRecord.Barcode)]));

        Assert.Null(uniqueIndex);
    }

    [Fact]
    public void ProductRecordShouldHaveExpectedIndexes()
    {
        var entityType = BuildModel().FindEntityType(typeof(ProductRecord))!;
        var indexPropertySets = entityType.GetIndexes()
            .Where(index => !index.IsUnique)
            .Select(index => index.Properties.Select(p => p.Name).ToArray())
            .ToArray();

        Assert.Contains(indexPropertySets, set => set.SequenceEqual([nameof(ProductRecord.OrganizationId)]));
        Assert.Contains(indexPropertySets, set => set.SequenceEqual([nameof(ProductRecord.IsActive)]));
        Assert.Contains(indexPropertySets, set => set.SequenceEqual([nameof(ProductRecord.Name)]));
        Assert.Contains(
            indexPropertySets,
            set => set.SequenceEqual([nameof(ProductRecord.OrganizationId), nameof(ProductRecord.Barcode)]));
    }

    // ---------- RoleRecord ----------

    [Fact]
    public void RoleRecordShouldMapToRolesTable()
    {
        var entityType = BuildModel().FindEntityType(typeof(RoleRecord))!;

        Assert.Equal("roles", entityType.GetTableName());
    }

    [Fact]
    public void RoleRecordShouldHaveIdAsPrimaryKey()
    {
        var entityType = BuildModel().FindEntityType(typeof(RoleRecord))!;
        var primaryKey = entityType.FindPrimaryKey()!;

        var keyPropertyName = Assert.Single(primaryKey.Properties).Name;
        Assert.Equal(nameof(RoleRecord.Id), keyPropertyName);
    }

    [Fact]
    public void RoleRecordShouldHaveExpectedColumnNames()
    {
        var entityType = BuildModel().FindEntityType(typeof(RoleRecord))!;

        Assert.Equal("id", entityType.FindProperty(nameof(RoleRecord.Id))!.GetColumnName());
        Assert.Equal(
            "organization_id",
            entityType.FindProperty(nameof(RoleRecord.OrganizationId))!.GetColumnName());
        Assert.Equal("name", entityType.FindProperty(nameof(RoleRecord.Name))!.GetColumnName());
        Assert.Equal("is_active", entityType.FindProperty(nameof(RoleRecord.IsActive))!.GetColumnName());
        Assert.Equal(
            "created_at_utc_ticks",
            entityType.FindProperty(nameof(RoleRecord.CreatedAtUtc))!.GetColumnName());
    }

    [Fact]
    public void RoleRecordNameShouldHaveMaxLength80()
    {
        var entityType = BuildModel().FindEntityType(typeof(RoleRecord))!;

        Assert.Equal(80, entityType.FindProperty(nameof(RoleRecord.Name))!.GetMaxLength());
    }

    [Fact]
    public void RoleRecordCreatedAtShouldBeConvertedToLong()
    {
        var entityType = BuildModel().FindEntityType(typeof(RoleRecord))!;
        var createdAt = entityType.FindProperty(nameof(RoleRecord.CreatedAtUtc))!;

        Assert.Equal(typeof(long), createdAt.GetValueConverter()!.ProviderClrType);
    }

    [Fact]
    public void RoleRecordShouldHaveRestrictForeignKeyToOrganizations()
    {
        var entityType = BuildModel().FindEntityType(typeof(RoleRecord))!;
        var foreignKey = Assert.Single(entityType.GetForeignKeys());

        Assert.Equal(typeof(OrganizationRecord), foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
        Assert.Equal(nameof(RoleRecord.OrganizationId), Assert.Single(foreignKey.Properties).Name);
    }

    [Fact]
    public void RoleRecordShouldHaveExpectedIndexes()
    {
        var entityType = BuildModel().FindEntityType(typeof(RoleRecord))!;
        var indexPropertySets = entityType.GetIndexes()
            .Select(index => index.Properties.Select(p => p.Name).ToArray())
            .ToArray();

        Assert.Contains(indexPropertySets, set => set.SequenceEqual([nameof(RoleRecord.OrganizationId)]));
        Assert.Contains(indexPropertySets, set => set.SequenceEqual([nameof(RoleRecord.IsActive)]));
    }

    [Fact]
    public void RoleRecordShouldNotHaveUniqueIndexOnOrganizationIdAndName()
    {
        var entityType = BuildModel().FindEntityType(typeof(RoleRecord))!;

        var uniqueIndex = entityType.GetIndexes().SingleOrDefault(index =>
            index.IsUnique &&
            index.Properties.Select(p => p.Name).SequenceEqual(
            [
                nameof(RoleRecord.OrganizationId),
                nameof(RoleRecord.Name),
            ]));

        Assert.Null(uniqueIndex);
    }

    // ---------- RolePermissionRecord ----------

    [Fact]
    public void RolePermissionRecordShouldMapToRolePermissionsTable()
    {
        var entityType = BuildModel().FindEntityType(typeof(RolePermissionRecord))!;

        Assert.Equal("role_permissions", entityType.GetTableName());
    }

    [Fact]
    public void RolePermissionRecordShouldHaveCompositePrimaryKey()
    {
        var entityType = BuildModel().FindEntityType(typeof(RolePermissionRecord))!;
        var primaryKey = entityType.FindPrimaryKey()!;

        var keyPropertyNames = primaryKey.Properties.Select(p => p.Name).ToArray();
        Assert.Equal(
            [nameof(RolePermissionRecord.RoleId), nameof(RolePermissionRecord.Permission)],
            keyPropertyNames);
    }

    [Fact]
    public void RolePermissionRecordShouldHaveExpectedColumnNames()
    {
        var entityType = BuildModel().FindEntityType(typeof(RolePermissionRecord))!;

        Assert.Equal("role_id", entityType.FindProperty(nameof(RolePermissionRecord.RoleId))!.GetColumnName());
        Assert.Equal(
            "permission",
            entityType.FindProperty(nameof(RolePermissionRecord.Permission))!.GetColumnName());
    }

    [Fact]
    public void RolePermissionRecordPermissionShouldHaveMaxLength80()
    {
        var entityType = BuildModel().FindEntityType(typeof(RolePermissionRecord))!;

        Assert.Equal(80, entityType.FindProperty(nameof(RolePermissionRecord.Permission))!.GetMaxLength());
    }

    [Fact]
    public void RolePermissionRecordShouldHaveCascadeForeignKeyToRoles()
    {
        var entityType = BuildModel().FindEntityType(typeof(RolePermissionRecord))!;
        var foreignKey = Assert.Single(entityType.GetForeignKeys());

        Assert.Equal(typeof(RoleRecord), foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
        Assert.Equal(nameof(RolePermissionRecord.RoleId), Assert.Single(foreignKey.Properties).Name);
    }

    // ---------- UserRecord ----------

    [Fact]
    public void UserRecordShouldMapToUsersTable()
    {
        var entityType = BuildModel().FindEntityType(typeof(UserRecord))!;

        Assert.Equal("users", entityType.GetTableName());
    }

    [Fact]
    public void UserRecordShouldHaveIdAsPrimaryKey()
    {
        var entityType = BuildModel().FindEntityType(typeof(UserRecord))!;
        var primaryKey = entityType.FindPrimaryKey()!;

        var keyPropertyName = Assert.Single(primaryKey.Properties).Name;
        Assert.Equal(nameof(UserRecord.Id), keyPropertyName);
    }

    [Fact]
    public void UserRecordShouldHaveExpectedColumnNames()
    {
        var entityType = BuildModel().FindEntityType(typeof(UserRecord))!;

        Assert.Equal("id", entityType.FindProperty(nameof(UserRecord.Id))!.GetColumnName());
        Assert.Equal(
            "organization_id",
            entityType.FindProperty(nameof(UserRecord.OrganizationId))!.GetColumnName());
        Assert.Equal("role_id", entityType.FindProperty(nameof(UserRecord.RoleId))!.GetColumnName());
        Assert.Equal("username", entityType.FindProperty(nameof(UserRecord.Username))!.GetColumnName());
        Assert.Equal(
            "display_name",
            entityType.FindProperty(nameof(UserRecord.DisplayName))!.GetColumnName());
        Assert.Equal("is_active", entityType.FindProperty(nameof(UserRecord.IsActive))!.GetColumnName());
        Assert.Equal(
            "created_at_utc_ticks",
            entityType.FindProperty(nameof(UserRecord.CreatedAtUtc))!.GetColumnName());
    }

    [Fact]
    public void UserRecordShouldHaveExpectedLengths()
    {
        var entityType = BuildModel().FindEntityType(typeof(UserRecord))!;

        Assert.Equal(40, entityType.FindProperty(nameof(UserRecord.Username))!.GetMaxLength());
        Assert.Equal(120, entityType.FindProperty(nameof(UserRecord.DisplayName))!.GetMaxLength());
    }

    [Fact]
    public void UserRecordCreatedAtShouldBeConvertedToLong()
    {
        var entityType = BuildModel().FindEntityType(typeof(UserRecord))!;
        var createdAt = entityType.FindProperty(nameof(UserRecord.CreatedAtUtc))!;

        Assert.Equal(typeof(long), createdAt.GetValueConverter()!.ProviderClrType);
    }

    [Fact]
    public void UserRecordShouldHaveRestrictForeignKeyToOrganizations()
    {
        var entityType = BuildModel().FindEntityType(typeof(UserRecord))!;
        var foreignKey = entityType.GetForeignKeys()
            .Single(fk => fk.PrincipalEntityType.ClrType == typeof(OrganizationRecord));

        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
        Assert.Equal(nameof(UserRecord.OrganizationId), Assert.Single(foreignKey.Properties).Name);
    }

    [Fact]
    public void UserRecordShouldHaveRestrictForeignKeyToRoles()
    {
        var entityType = BuildModel().FindEntityType(typeof(UserRecord))!;
        var foreignKey = entityType.GetForeignKeys()
            .Single(fk => fk.PrincipalEntityType.ClrType == typeof(RoleRecord));

        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
        Assert.Equal(nameof(UserRecord.RoleId), Assert.Single(foreignKey.Properties).Name);
    }

    [Fact]
    public void UserRecordShouldHaveExpectedIndexes()
    {
        var entityType = BuildModel().FindEntityType(typeof(UserRecord))!;
        var indexPropertySets = entityType.GetIndexes()
            .Select(index => index.Properties.Select(p => p.Name).ToArray())
            .ToArray();

        Assert.Contains(indexPropertySets, set => set.SequenceEqual([nameof(UserRecord.OrganizationId)]));
        Assert.Contains(indexPropertySets, set => set.SequenceEqual([nameof(UserRecord.RoleId)]));
        Assert.Contains(indexPropertySets, set => set.SequenceEqual([nameof(UserRecord.IsActive)]));
        Assert.Contains(
            indexPropertySets,
            set => set.SequenceEqual([nameof(UserRecord.OrganizationId), nameof(UserRecord.Username)]));
    }

    [Fact]
    public void UserRecordShouldNotHaveUniqueIndexOnOrganizationIdAndUsername()
    {
        var entityType = BuildModel().FindEntityType(typeof(UserRecord))!;

        var uniqueIndex = entityType.GetIndexes().SingleOrDefault(index =>
            index.IsUnique &&
            index.Properties.Select(p => p.Name).SequenceEqual(
            [
                nameof(UserRecord.OrganizationId),
                nameof(UserRecord.Username),
            ]));

        Assert.Null(uniqueIndex);
    }

    // ---------- RegisterSessionRecord ----------

    [Fact]
    public void RegisterSessionRecordShouldMapToRegisterSessionsTable()
    {
        var entityType = BuildModel().FindEntityType(typeof(RegisterSessionRecord))!;

        Assert.Equal("register_sessions", entityType.GetTableName());
    }

    [Fact]
    public void RegisterSessionRecordShouldHaveIdAsPrimaryKey()
    {
        var entityType = BuildModel().FindEntityType(typeof(RegisterSessionRecord))!;
        var primaryKey = entityType.FindPrimaryKey()!;

        var keyPropertyName = Assert.Single(primaryKey.Properties).Name;
        Assert.Equal(nameof(RegisterSessionRecord.Id), keyPropertyName);
    }

    [Fact]
    public void RegisterSessionRecordShouldHaveExpectedColumnNames()
    {
        var entityType = BuildModel().FindEntityType(typeof(RegisterSessionRecord))!;

        Assert.Equal("id", entityType.FindProperty(nameof(RegisterSessionRecord.Id))!.GetColumnName());
        Assert.Equal(
            "register_id",
            entityType.FindProperty(nameof(RegisterSessionRecord.RegisterId))!.GetColumnName());
        Assert.Equal(
            "opened_by_user_id",
            entityType.FindProperty(nameof(RegisterSessionRecord.OpenedByUserId))!.GetColumnName());
        Assert.Equal(
            "closed_by_user_id",
            entityType.FindProperty(nameof(RegisterSessionRecord.ClosedByUserId))!.GetColumnName());
        Assert.Equal(
            "opening_float_amount",
            entityType.FindProperty(nameof(RegisterSessionRecord.OpeningFloatAmount))!.GetColumnName());
        Assert.Equal(
            "opening_float_currency",
            entityType.FindProperty(nameof(RegisterSessionRecord.OpeningFloatCurrency))!.GetColumnName());
        Assert.Equal(
            "expected_cash_amount",
            entityType.FindProperty(nameof(RegisterSessionRecord.ExpectedCashAmount))!.GetColumnName());
        Assert.Equal(
            "counted_cash_amount",
            entityType.FindProperty(nameof(RegisterSessionRecord.CountedCashAmount))!.GetColumnName());
        Assert.Equal(
            "cash_difference_amount",
            entityType.FindProperty(nameof(RegisterSessionRecord.CashDifferenceAmount))!.GetColumnName());
        Assert.Equal("status", entityType.FindProperty(nameof(RegisterSessionRecord.Status))!.GetColumnName());
        Assert.Equal(
            "opened_at_utc_ticks",
            entityType.FindProperty(nameof(RegisterSessionRecord.OpenedAtUtc))!.GetColumnName());
        Assert.Equal(
            "closed_at_utc_ticks",
            entityType.FindProperty(nameof(RegisterSessionRecord.ClosedAtUtc))!.GetColumnName());
    }

    [Fact]
    public void RegisterSessionRecordStatusShouldBeConvertedToStringWithMaxLength32()
    {
        var entityType = BuildModel().FindEntityType(typeof(RegisterSessionRecord))!;
        var status = entityType.FindProperty(nameof(RegisterSessionRecord.Status))!;

        Assert.Equal(typeof(string), status.GetValueConverter()!.ProviderClrType);
        Assert.Equal(32, status.GetMaxLength());
    }

    [Fact]
    public void RegisterSessionRecordDatesShouldBeConvertedToLong()
    {
        var entityType = BuildModel().FindEntityType(typeof(RegisterSessionRecord))!;

        var openedAt = entityType.FindProperty(nameof(RegisterSessionRecord.OpenedAtUtc))!;
        var closedAt = entityType.FindProperty(nameof(RegisterSessionRecord.ClosedAtUtc))!;

        Assert.Equal(typeof(long), openedAt.GetValueConverter()!.ProviderClrType);
        Assert.Equal(typeof(long), closedAt.GetValueConverter()!.ProviderClrType);
    }

    [Fact]
    public void RegisterSessionRecordCloseFieldsShouldBeNullable()
    {
        var entityType = BuildModel().FindEntityType(typeof(RegisterSessionRecord))!;

        Assert.True(entityType.FindProperty(nameof(RegisterSessionRecord.ClosedByUserId))!.IsNullable);
        Assert.True(entityType.FindProperty(nameof(RegisterSessionRecord.ExpectedCashAmount))!.IsNullable);
        Assert.True(entityType.FindProperty(nameof(RegisterSessionRecord.CountedCashAmount))!.IsNullable);
        Assert.True(entityType.FindProperty(nameof(RegisterSessionRecord.CashDifferenceAmount))!.IsNullable);
        Assert.True(entityType.FindProperty(nameof(RegisterSessionRecord.ClosedAtUtc))!.IsNullable);
        Assert.False(entityType.FindProperty(nameof(RegisterSessionRecord.OpeningFloatAmount))!.IsNullable);
        Assert.False(entityType.FindProperty(nameof(RegisterSessionRecord.OpenedAtUtc))!.IsNullable);
    }

    [Fact]
    public void RegisterSessionRecordAmountsShouldHavePrecision18And2()
    {
        var entityType = BuildModel().FindEntityType(typeof(RegisterSessionRecord))!;

        var openingFloat = entityType.FindProperty(nameof(RegisterSessionRecord.OpeningFloatAmount))!;
        Assert.Equal(18, openingFloat.GetPrecision());
        Assert.Equal(2, openingFloat.GetScale());

        var cashDifference = entityType.FindProperty(nameof(RegisterSessionRecord.CashDifferenceAmount))!;
        Assert.Equal(18, cashDifference.GetPrecision());
        Assert.Equal(2, cashDifference.GetScale());
    }

    [Fact]
    public void RegisterSessionRecordCurrenciesShouldHaveMaxLength3()
    {
        var entityType = BuildModel().FindEntityType(typeof(RegisterSessionRecord))!;

        Assert.Equal(3, entityType.FindProperty(nameof(RegisterSessionRecord.OpeningFloatCurrency))!.GetMaxLength());
        Assert.Equal(3, entityType.FindProperty(nameof(RegisterSessionRecord.ExpectedCashCurrency))!.GetMaxLength());
        Assert.Equal(3, entityType.FindProperty(nameof(RegisterSessionRecord.CountedCashCurrency))!.GetMaxLength());
        Assert.Equal(
            3, entityType.FindProperty(nameof(RegisterSessionRecord.CashDifferenceCurrency))!.GetMaxLength());
    }

    [Fact]
    public void RegisterSessionRecordShouldHaveRestrictForeignKeyToRegisters()
    {
        var entityType = BuildModel().FindEntityType(typeof(RegisterSessionRecord))!;
        var foreignKey = entityType.GetForeignKeys()
            .Single(fk => fk.PrincipalEntityType.ClrType == typeof(RegisterRecord));

        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
        Assert.Equal(nameof(RegisterSessionRecord.RegisterId), Assert.Single(foreignKey.Properties).Name);
    }

    [Fact]
    public void RegisterSessionRecordShouldHaveTwoRestrictForeignKeysToUsers()
    {
        var entityType = BuildModel().FindEntityType(typeof(RegisterSessionRecord))!;
        var foreignKeysToUsers = entityType.GetForeignKeys()
            .Where(fk => fk.PrincipalEntityType.ClrType == typeof(UserRecord))
            .ToArray();

        Assert.Equal(2, foreignKeysToUsers.Length);
        Assert.All(foreignKeysToUsers, fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));

        Assert.Contains(
            foreignKeysToUsers,
            fk => fk.Properties.Select(p => p.Name).SequenceEqual([nameof(RegisterSessionRecord.OpenedByUserId)]));
        Assert.Contains(
            foreignKeysToUsers,
            fk => fk.Properties.Select(p => p.Name).SequenceEqual([nameof(RegisterSessionRecord.ClosedByUserId)]));
    }

    [Fact]
    public void RegisterSessionRecordClosedByUserIdForeignKeyShouldBeOptional()
    {
        var entityType = BuildModel().FindEntityType(typeof(RegisterSessionRecord))!;
        var foreignKey = entityType.GetForeignKeys()
            .Single(fk =>
                fk.PrincipalEntityType.ClrType == typeof(UserRecord) &&
                fk.Properties.Select(p => p.Name).SequenceEqual([nameof(RegisterSessionRecord.ClosedByUserId)]));

        Assert.False(foreignKey.IsRequired);
    }

    [Fact]
    public void RegisterSessionRecordShouldHaveExpectedIndexes()
    {
        var entityType = BuildModel().FindEntityType(typeof(RegisterSessionRecord))!;
        var indexPropertySets = entityType.GetIndexes()
            .Select(index => index.Properties.Select(p => p.Name).ToArray())
            .ToArray();

        Assert.Contains(indexPropertySets, set => set.SequenceEqual([nameof(RegisterSessionRecord.RegisterId)]));
        Assert.Contains(
            indexPropertySets, set => set.SequenceEqual([nameof(RegisterSessionRecord.OpenedByUserId)]));
        Assert.Contains(
            indexPropertySets, set => set.SequenceEqual([nameof(RegisterSessionRecord.ClosedByUserId)]));
        Assert.Contains(indexPropertySets, set => set.SequenceEqual([nameof(RegisterSessionRecord.Status)]));
        Assert.Contains(indexPropertySets, set => set.SequenceEqual([nameof(RegisterSessionRecord.OpenedAtUtc)]));
        Assert.Contains(indexPropertySets, set => set.SequenceEqual([nameof(RegisterSessionRecord.ClosedAtUtc)]));
    }

    [Fact]
    public void RegisterSessionRecordShouldNotHaveUniqueIndexOnRegisterIdAndStatus()
    {
        var entityType = BuildModel().FindEntityType(typeof(RegisterSessionRecord))!;

        var uniqueIndex = entityType.GetIndexes().SingleOrDefault(index =>
            index.IsUnique &&
            index.Properties.Select(p => p.Name).SequenceEqual(
            [
                nameof(RegisterSessionRecord.RegisterId),
                nameof(RegisterSessionRecord.Status),
            ]));

        Assert.Null(uniqueIndex);
    }
}
