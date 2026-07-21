using Pos.Infrastructure.Persistence.Exceptions;
using Pos.Infrastructure.Persistence.Mappers;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Tests.Persistence.Mappers;

public class InventoryItemMapperTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset UpdatedAtUtc = new(2026, 1, 2, 9, 30, 0, TimeSpan.Zero);

    private static InventoryItemRecord CreateValidRecord() => new()
    {
        Id = Guid.NewGuid(),
        BranchId = Guid.NewGuid(),
        ProductId = Guid.NewGuid(),
        Quantity = 12.5m,
        ReorderPoint = 3m,
        CreatedAtUtc = CreatedAtUtc,
        UpdatedAtUtc = UpdatedAtUtc,
    };

    // ---------- ToDomain ----------

    [Fact]
    public void ToDomainReconstructsIdentifiers()
    {
        var record = CreateValidRecord();

        var item = InventoryItemMapper.ToDomain(record);

        Assert.Equal(record.Id, item.Id.Value);
        Assert.Equal(record.BranchId, item.BranchId.Value);
        Assert.Equal(record.ProductId, item.ProductId.Value);
    }

    [Fact]
    public void ToDomainKeepsQuantity()
    {
        var record = CreateValidRecord();

        var item = InventoryItemMapper.ToDomain(record);

        Assert.Equal(record.Quantity, item.Quantity);
    }

    [Fact]
    public void ToDomainKeepsReorderPoint()
    {
        var record = CreateValidRecord();

        var item = InventoryItemMapper.ToDomain(record);

        Assert.Equal(record.ReorderPoint, item.ReorderPoint);
    }

    [Fact]
    public void ToDomainKeepsCreatedAtUtc()
    {
        var record = CreateValidRecord();

        var item = InventoryItemMapper.ToDomain(record);

        Assert.Equal(CreatedAtUtc, item.CreatedAtUtc);
    }

    [Fact]
    public void ToDomainKeepsUpdatedAtUtcWhenPosteriorToCreatedAtUtc()
    {
        var record = CreateValidRecord();

        var item = InventoryItemMapper.ToDomain(record);

        Assert.Equal(UpdatedAtUtc, item.UpdatedAtUtc);
    }

    [Fact]
    public void ToDomainReturnsZeroOffset()
    {
        var record = CreateValidRecord();

        var item = InventoryItemMapper.ToDomain(record);

        Assert.Equal(TimeSpan.Zero, item.CreatedAtUtc.Offset);
        Assert.Equal(TimeSpan.Zero, item.UpdatedAtUtc.Offset);
    }

    [Fact]
    public void ToDomainRejectsNullRecord()
    {
        Assert.Throws<ArgumentNullException>(() => InventoryItemMapper.ToDomain(null!));
    }

    [Fact]
    public void ToDomainWrapsNegativeQuantityInPersistenceDataException()
    {
        var record = CreateValidRecord();
        record.Quantity = -1m;

        Assert.Throws<PersistenceDataException>(() => InventoryItemMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsEmptyIdInPersistenceDataException()
    {
        var record = CreateValidRecord();
        record.Id = Guid.Empty;

        Assert.Throws<PersistenceDataException>(() => InventoryItemMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsEmptyBranchIdInPersistenceDataException()
    {
        var record = CreateValidRecord();
        record.BranchId = Guid.Empty;

        Assert.Throws<PersistenceDataException>(() => InventoryItemMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsEmptyProductIdInPersistenceDataException()
    {
        var record = CreateValidRecord();
        record.ProductId = Guid.Empty;

        Assert.Throws<PersistenceDataException>(() => InventoryItemMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsUpdatedAtUtcBeforeCreatedAtUtcInPersistenceDataException()
    {
        var record = CreateValidRecord();
        record.UpdatedAtUtc = CreatedAtUtc.AddDays(-1);

        Assert.Throws<PersistenceDataException>(() => InventoryItemMapper.ToDomain(record));
    }

    // ---------- ToRecord ----------

    [Fact]
    public void ToRecordKeepsAllFields()
    {
        var item = InventoryItemMapper.ToDomain(CreateValidRecord());

        var record = InventoryItemMapper.ToRecord(item);

        Assert.Equal(item.Id.Value, record.Id);
        Assert.Equal(item.BranchId.Value, record.BranchId);
        Assert.Equal(item.ProductId.Value, record.ProductId);
        Assert.Equal(item.Quantity, record.Quantity);
        Assert.Equal(item.ReorderPoint, record.ReorderPoint);
        Assert.Equal(item.CreatedAtUtc, record.CreatedAtUtc);
        Assert.Equal(item.UpdatedAtUtc, record.UpdatedAtUtc);
    }

    [Fact]
    public void ToRecordRejectsNullInventoryItem()
    {
        Assert.Throws<ArgumentNullException>(() => InventoryItemMapper.ToRecord(null!));
    }

    // ---------- UpdateRecord ----------

    [Fact]
    public void UpdateRecordUpdatesQuantity()
    {
        var record = CreateValidRecord();
        var item = InventoryItemMapper.ToDomain(record);
        item.Increase(5m, UpdatedAtUtc.AddHours(1));

        InventoryItemMapper.UpdateRecord(item, record);

        Assert.Equal(item.Quantity, record.Quantity);
    }

    [Fact]
    public void UpdateRecordUpdatesReorderPoint()
    {
        var record = CreateValidRecord();
        var item = InventoryItemMapper.ToDomain(record);
        item.ChangeReorderPoint(9m, UpdatedAtUtc.AddHours(1));

        InventoryItemMapper.UpdateRecord(item, record);

        Assert.Equal(9m, record.ReorderPoint);
    }

    [Fact]
    public void UpdateRecordUpdatesUpdatedAtUtc()
    {
        var record = CreateValidRecord();
        var item = InventoryItemMapper.ToDomain(record);
        var newUpdatedAtUtc = UpdatedAtUtc.AddHours(2);
        item.Increase(1m, newUpdatedAtUtc);

        InventoryItemMapper.UpdateRecord(item, record);

        Assert.Equal(newUpdatedAtUtc, record.UpdatedAtUtc);
    }

    [Fact]
    public void UpdateRecordDoesNotChangeId()
    {
        var record = CreateValidRecord();
        var originalId = record.Id;
        var item = InventoryItemMapper.ToDomain(record);

        InventoryItemMapper.UpdateRecord(item, record);

        Assert.Equal(originalId, record.Id);
    }

    [Fact]
    public void UpdateRecordDoesNotChangeBranchId()
    {
        var record = CreateValidRecord();
        var originalBranchId = record.BranchId;
        var item = InventoryItemMapper.ToDomain(record);

        InventoryItemMapper.UpdateRecord(item, record);

        Assert.Equal(originalBranchId, record.BranchId);
    }

    [Fact]
    public void UpdateRecordDoesNotChangeProductId()
    {
        var record = CreateValidRecord();
        var originalProductId = record.ProductId;
        var item = InventoryItemMapper.ToDomain(record);

        InventoryItemMapper.UpdateRecord(item, record);

        Assert.Equal(originalProductId, record.ProductId);
    }

    [Fact]
    public void UpdateRecordDoesNotChangeCreatedAtUtc()
    {
        var record = CreateValidRecord();
        var item = InventoryItemMapper.ToDomain(record);

        InventoryItemMapper.UpdateRecord(item, record);

        Assert.Equal(CreatedAtUtc, record.CreatedAtUtc);
    }

    [Fact]
    public void UpdateRecordRejectsMismatchedId()
    {
        var record = CreateValidRecord();
        var item = InventoryItemMapper.ToDomain(record);
        record.Id = Guid.NewGuid();

        Assert.Throws<PersistenceDataException>(() => InventoryItemMapper.UpdateRecord(item, record));
    }

    [Fact]
    public void UpdateRecordRejectsMismatchedBranchId()
    {
        var record = CreateValidRecord();
        var item = InventoryItemMapper.ToDomain(record);
        record.BranchId = Guid.NewGuid();

        Assert.Throws<PersistenceDataException>(() => InventoryItemMapper.UpdateRecord(item, record));
    }

    [Fact]
    public void UpdateRecordRejectsMismatchedProductId()
    {
        var record = CreateValidRecord();
        var item = InventoryItemMapper.ToDomain(record);
        record.ProductId = Guid.NewGuid();

        Assert.Throws<PersistenceDataException>(() => InventoryItemMapper.UpdateRecord(item, record));
    }

    [Fact]
    public void UpdateRecordRejectsMismatchedCreatedAtUtc()
    {
        var record = CreateValidRecord();
        var item = InventoryItemMapper.ToDomain(record);
        record.CreatedAtUtc = CreatedAtUtc.AddDays(1);

        Assert.Throws<PersistenceDataException>(() => InventoryItemMapper.UpdateRecord(item, record));
    }

    [Fact]
    public void UpdateRecordRejectsNullInventoryItem()
    {
        var record = CreateValidRecord();

        Assert.Throws<ArgumentNullException>(() => InventoryItemMapper.UpdateRecord(null!, record));
    }

    [Fact]
    public void UpdateRecordRejectsNullRecord()
    {
        var item = InventoryItemMapper.ToDomain(CreateValidRecord());

        Assert.Throws<ArgumentNullException>(() => InventoryItemMapper.UpdateRecord(item, null!));
    }
}
