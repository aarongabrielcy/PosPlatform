using Pos.Domain.Common.Identifiers;
using Pos.Domain.Inventory;
using Pos.Infrastructure.Persistence.Exceptions;
using Pos.Infrastructure.Persistence.Mappers;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Tests.Persistence.Mappers;

public class InventoryMovementMapperTests
{
    private static readonly DateTimeOffset OccurredAtUtc = new(2026, 1, 1, 9, 30, 0, TimeSpan.Zero);

    private static InventoryMovement CreateManualIncrease() =>
        InventoryMovement.CreateManualIncrease(
            InventoryMovementId.New(),
            InventoryItemId.New(),
            BranchId.New(),
            ProductId.New(),
            UserId.New(),
            quantity: 5m,
            quantityBefore: 10m,
            OccurredAtUtc);

    private static InventoryMovement CreateManualDecrease() =>
        InventoryMovement.CreateManualDecrease(
            InventoryMovementId.New(),
            InventoryItemId.New(),
            BranchId.New(),
            ProductId.New(),
            UserId.New(),
            quantity: 4m,
            quantityBefore: 10m,
            OccurredAtUtc);

    private static InventoryMovement CreateSaleDecrease() =>
        InventoryMovement.CreateSaleDecrease(
            InventoryMovementId.New(),
            InventoryItemId.New(),
            BranchId.New(),
            ProductId.New(),
            UserId.New(),
            SaleId.New(),
            SaleLineId.New(),
            quantity: 3m,
            quantityBefore: 10m,
            OccurredAtUtc);

    private static void AssertRoundTrip(InventoryMovement movement)
    {
        var record = InventoryMovementMapper.ToRecord(movement);
        var reconstructed = InventoryMovementMapper.ToDomain(record);

        Assert.Equal(movement.Id, reconstructed.Id);
        Assert.Equal(movement.InventoryItemId, reconstructed.InventoryItemId);
        Assert.Equal(movement.BranchId, reconstructed.BranchId);
        Assert.Equal(movement.ProductId, reconstructed.ProductId);
        Assert.Equal(movement.PerformedByUserId, reconstructed.PerformedByUserId);
        Assert.Equal(movement.Type, reconstructed.Type);
        Assert.Equal(movement.Quantity, reconstructed.Quantity);
        Assert.Equal(movement.QuantityBefore, reconstructed.QuantityBefore);
        Assert.Equal(movement.QuantityAfter, reconstructed.QuantityAfter);
        Assert.Equal(movement.SaleId, reconstructed.SaleId);
        Assert.Equal(movement.SaleLineId, reconstructed.SaleLineId);
        Assert.Equal(movement.OccurredAtUtc, reconstructed.OccurredAtUtc);
    }

    // ---------- Round-trip ----------

    [Fact]
    public void RoundTripPreservesManualIncrease() => AssertRoundTrip(CreateManualIncrease());

    [Fact]
    public void RoundTripPreservesManualDecrease() => AssertRoundTrip(CreateManualDecrease());

    [Fact]
    public void RoundTripPreservesSaleDecrease() => AssertRoundTrip(CreateSaleDecrease());

    [Fact]
    public void ToRecordPreservesSaleDecreaseSaleReferences()
    {
        var movement = CreateSaleDecrease();

        var record = InventoryMovementMapper.ToRecord(movement);

        Assert.Equal(movement.SaleId!.Value.Value, record.SaleId);
        Assert.Equal(movement.SaleLineId!.Value.Value, record.SaleLineId);
    }

    [Fact]
    public void ToRecordLeavesManualDecreaseSaleReferencesNull()
    {
        var movement = CreateManualDecrease();

        var record = InventoryMovementMapper.ToRecord(movement);

        Assert.Null(record.SaleId);
        Assert.Null(record.SaleLineId);
    }

    [Fact]
    public void ToRecordRejectsNullMovement()
    {
        Assert.Throws<ArgumentNullException>(() => InventoryMovementMapper.ToRecord(null!));
    }

    [Fact]
    public void ToDomainRejectsNullRecord()
    {
        Assert.Throws<ArgumentNullException>(() => InventoryMovementMapper.ToDomain(null!));
    }

    // ---------- Datos corruptos ----------

    [Fact]
    public void ToDomainThrowsWhenQuantityAfterDoesNotMatchDomainCalculation()
    {
        var record = InventoryMovementMapper.ToRecord(CreateManualIncrease());
        record.QuantityAfter += 1m;

        Assert.Throws<PersistenceDataException>(() => InventoryMovementMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainThrowsWhenIdIsEmpty()
    {
        var record = InventoryMovementMapper.ToRecord(CreateManualIncrease());
        record.Id = Guid.Empty;

        Assert.Throws<PersistenceDataException>(() => InventoryMovementMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainThrowsWhenInventoryItemIdIsEmpty()
    {
        var record = InventoryMovementMapper.ToRecord(CreateManualIncrease());
        record.InventoryItemId = Guid.Empty;

        Assert.Throws<PersistenceDataException>(() => InventoryMovementMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainThrowsWhenBranchIdIsEmpty()
    {
        var record = InventoryMovementMapper.ToRecord(CreateManualIncrease());
        record.BranchId = Guid.Empty;

        Assert.Throws<PersistenceDataException>(() => InventoryMovementMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainThrowsWhenProductIdIsEmpty()
    {
        var record = InventoryMovementMapper.ToRecord(CreateManualIncrease());
        record.ProductId = Guid.Empty;

        Assert.Throws<PersistenceDataException>(() => InventoryMovementMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainThrowsWhenPerformedByUserIdIsEmpty()
    {
        var record = InventoryMovementMapper.ToRecord(CreateManualIncrease());
        record.PerformedByUserId = Guid.Empty;

        Assert.Throws<PersistenceDataException>(() => InventoryMovementMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainThrowsWhenSaleDecreaseHasEmptySaleId()
    {
        var record = InventoryMovementMapper.ToRecord(CreateSaleDecrease());
        record.SaleId = Guid.Empty;

        Assert.Throws<PersistenceDataException>(() => InventoryMovementMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainThrowsWhenSaleDecreaseHasEmptySaleLineId()
    {
        var record = InventoryMovementMapper.ToRecord(CreateSaleDecrease());
        record.SaleLineId = Guid.Empty;

        Assert.Throws<PersistenceDataException>(() => InventoryMovementMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainThrowsWhenTypeIsUndefined()
    {
        var record = InventoryMovementMapper.ToRecord(CreateManualIncrease());
        record.Type = (InventoryMovementType)99;

        Assert.Throws<PersistenceDataException>(() => InventoryMovementMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainThrowsWhenManualIncreaseHasSaleId()
    {
        var record = InventoryMovementMapper.ToRecord(CreateManualIncrease());
        record.SaleId = Guid.NewGuid();

        Assert.Throws<PersistenceDataException>(() => InventoryMovementMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainThrowsWhenManualDecreaseHasSaleLineId()
    {
        var record = InventoryMovementMapper.ToRecord(CreateManualDecrease());
        record.SaleLineId = Guid.NewGuid();

        Assert.Throws<PersistenceDataException>(() => InventoryMovementMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainThrowsWhenSaleDecreaseIsMissingSaleId()
    {
        var record = InventoryMovementMapper.ToRecord(CreateSaleDecrease());
        record.SaleId = null;

        Assert.Throws<PersistenceDataException>(() => InventoryMovementMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainThrowsWhenSaleDecreaseIsMissingSaleLineId()
    {
        var record = InventoryMovementMapper.ToRecord(CreateSaleDecrease());
        record.SaleLineId = null;

        Assert.Throws<PersistenceDataException>(() => InventoryMovementMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainThrowsWhenOccurredAtUtcIsNotUtc()
    {
        var record = InventoryMovementMapper.ToRecord(CreateManualIncrease());
        record.OccurredAtUtc = new DateTimeOffset(2026, 1, 1, 9, 30, 0, TimeSpan.FromHours(-5));

        Assert.Throws<PersistenceDataException>(() => InventoryMovementMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainThrowsWhenQuantityIsZero()
    {
        var record = InventoryMovementMapper.ToRecord(CreateManualIncrease());
        record.Quantity = 0m;
        record.QuantityAfter = record.QuantityBefore;

        Assert.Throws<PersistenceDataException>(() => InventoryMovementMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainThrowsWhenQuantityBeforeIsNegative()
    {
        var record = InventoryMovementMapper.ToRecord(CreateManualIncrease());
        record.QuantityBefore = -1m;
        record.QuantityAfter = record.QuantityBefore + record.Quantity;

        Assert.Throws<PersistenceDataException>(() => InventoryMovementMapper.ToDomain(record));
    }
}
