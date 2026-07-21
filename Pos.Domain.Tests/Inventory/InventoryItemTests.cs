using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Inventory;

namespace Pos.Domain.Tests.Inventory;

public class InventoryItemTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset LaterUtc = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    private static InventoryItem CreateItem(
        decimal initialQuantity = 10m,
        decimal reorderPoint = 2m,
        DateTimeOffset? createdAtUtc = null) =>
        new(
            InventoryItemId.New(),
            BranchId.New(),
            ProductId.New(),
            initialQuantity,
            reorderPoint,
            createdAtUtc ?? CreatedAtUtc);

    // ---------- Creación ----------

    [Fact]
    public void ConstructorKeepsId()
    {
        var id = InventoryItemId.New();

        var item = new InventoryItem(id, BranchId.New(), ProductId.New(), 10m, 2m, CreatedAtUtc);

        Assert.Equal(id, item.Id);
    }

    [Fact]
    public void ConstructorKeepsBranchId()
    {
        var branchId = BranchId.New();

        var item = new InventoryItem(InventoryItemId.New(), branchId, ProductId.New(), 10m, 2m, CreatedAtUtc);

        Assert.Equal(branchId, item.BranchId);
    }

    [Fact]
    public void ConstructorKeepsProductId()
    {
        var productId = ProductId.New();

        var item = new InventoryItem(InventoryItemId.New(), BranchId.New(), productId, 10m, 2m, CreatedAtUtc);

        Assert.Equal(productId, item.ProductId);
    }

    [Fact]
    public void ConstructorKeepsInitialQuantity()
    {
        var item = CreateItem(initialQuantity: 15m);

        Assert.Equal(15m, item.Quantity);
    }

    [Fact]
    public void ConstructorKeepsReorderPoint()
    {
        var item = CreateItem(reorderPoint: 5m);

        Assert.Equal(5m, item.ReorderPoint);
    }

    [Fact]
    public void ConstructorSetsCreatedAndUpdatedAtUtcEqual()
    {
        var item = CreateItem(createdAtUtc: CreatedAtUtc);

        Assert.Equal(CreatedAtUtc, item.CreatedAtUtc);
        Assert.Equal(CreatedAtUtc, item.UpdatedAtUtc);
    }

    [Fact]
    public void ConstructorAllowsZeroInitialQuantity()
    {
        var item = CreateItem(initialQuantity: 0m);

        Assert.Equal(0m, item.Quantity);
    }

    [Fact]
    public void ConstructorAllowsZeroReorderPoint()
    {
        var item = CreateItem(reorderPoint: 0m);

        Assert.Equal(0m, item.ReorderPoint);
    }

    [Fact]
    public void ConstructorAllowsDecimalQuantities()
    {
        var item = CreateItem(initialQuantity: 1.5m, reorderPoint: 0.25m);

        Assert.Equal(1.5m, item.Quantity);
        Assert.Equal(0.25m, item.ReorderPoint);
    }

    [Fact]
    public void ConstructorRejectsDefaultInventoryItemId()
    {
        Assert.Throws<DomainValidationException>(
            () => new InventoryItem(default, BranchId.New(), ProductId.New(), 10m, 2m, CreatedAtUtc));
    }

    [Fact]
    public void ConstructorRejectsDefaultBranchId()
    {
        Assert.Throws<DomainValidationException>(
            () => new InventoryItem(InventoryItemId.New(), default, ProductId.New(), 10m, 2m, CreatedAtUtc));
    }

    [Fact]
    public void ConstructorRejectsDefaultProductId()
    {
        Assert.Throws<DomainValidationException>(
            () => new InventoryItem(InventoryItemId.New(), BranchId.New(), default, 10m, 2m, CreatedAtUtc));
    }

    [Fact]
    public void ConstructorRejectsNegativeInitialQuantity()
    {
        Assert.Throws<DomainValidationException>(
            () => new InventoryItem(InventoryItemId.New(), BranchId.New(), ProductId.New(), -1m, 2m, CreatedAtUtc));
    }

    [Fact]
    public void ConstructorRejectsNegativeReorderPoint()
    {
        Assert.Throws<DomainValidationException>(
            () => new InventoryItem(InventoryItemId.New(), BranchId.New(), ProductId.New(), 10m, -1m, CreatedAtUtc));
    }

    [Fact]
    public void ConstructorRejectsNonUtcCreatedAtUtc()
    {
        var nonUtc = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.FromHours(-5));

        Assert.Throws<DomainValidationException>(
            () => new InventoryItem(InventoryItemId.New(), BranchId.New(), ProductId.New(), 10m, 2m, nonUtc));
    }

    // ---------- Increase ----------

    [Fact]
    public void IncreaseIncrementsQuantity()
    {
        var item = CreateItem(initialQuantity: 10m);

        item.Increase(5m, LaterUtc);

        Assert.Equal(15m, item.Quantity);
    }

    [Fact]
    public void IncreaseAllowsDecimalQuantity()
    {
        var item = CreateItem(initialQuantity: 10m);

        item.Increase(1.25m, LaterUtc);

        Assert.Equal(11.25m, item.Quantity);
    }

    [Fact]
    public void IncreaseUpdatesUpdatedAtUtc()
    {
        var item = CreateItem(createdAtUtc: CreatedAtUtc);

        item.Increase(5m, LaterUtc);

        Assert.Equal(LaterUtc, item.UpdatedAtUtc);
    }

    [Fact]
    public void IncreaseAllowsOccurredAtUtcEqualToUpdatedAtUtc()
    {
        var item = CreateItem(createdAtUtc: CreatedAtUtc);

        item.Increase(5m, CreatedAtUtc);

        Assert.Equal(CreatedAtUtc, item.UpdatedAtUtc);
    }

    [Fact]
    public void IncreaseRejectsZero()
    {
        var item = CreateItem();

        Assert.Throws<DomainValidationException>(() => item.Increase(0m, LaterUtc));
    }

    [Fact]
    public void IncreaseRejectsNegativeValue()
    {
        var item = CreateItem();

        Assert.Throws<DomainValidationException>(() => item.Increase(-1m, LaterUtc));
    }

    [Fact]
    public void IncreaseRejectsNonUtcDate()
    {
        var item = CreateItem();
        var nonUtc = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.FromHours(-5));

        Assert.Throws<DomainValidationException>(() => item.Increase(5m, nonUtc));
    }

    [Fact]
    public void IncreaseRejectsDateBeforeUpdatedAtUtc()
    {
        var item = CreateItem(createdAtUtc: LaterUtc);

        Assert.Throws<DomainValidationException>(() => item.Increase(5m, CreatedAtUtc));
    }

    // ---------- Decrease ----------

    [Fact]
    public void DecreaseDecrementsQuantity()
    {
        var item = CreateItem(initialQuantity: 10m);

        item.Decrease(4m, LaterUtc);

        Assert.Equal(6m, item.Quantity);
    }

    [Fact]
    public void DecreaseAllowsDecimalQuantity()
    {
        var item = CreateItem(initialQuantity: 10m);

        item.Decrease(1.5m, LaterUtc);

        Assert.Equal(8.5m, item.Quantity);
    }

    [Fact]
    public void DecreaseAllowsQuantityToReachZero()
    {
        var item = CreateItem(initialQuantity: 10m);

        item.Decrease(10m, LaterUtc);

        Assert.Equal(0m, item.Quantity);
    }

    [Fact]
    public void DecreaseUpdatesUpdatedAtUtc()
    {
        var item = CreateItem(initialQuantity: 10m, createdAtUtc: CreatedAtUtc);

        item.Decrease(4m, LaterUtc);

        Assert.Equal(LaterUtc, item.UpdatedAtUtc);
    }

    [Fact]
    public void DecreaseAllowsOccurredAtUtcEqualToUpdatedAtUtc()
    {
        var item = CreateItem(initialQuantity: 10m, createdAtUtc: CreatedAtUtc);

        item.Decrease(4m, CreatedAtUtc);

        Assert.Equal(CreatedAtUtc, item.UpdatedAtUtc);
    }

    [Fact]
    public void DecreaseRejectsZero()
    {
        var item = CreateItem(initialQuantity: 10m);

        Assert.Throws<DomainValidationException>(() => item.Decrease(0m, LaterUtc));
    }

    [Fact]
    public void DecreaseRejectsNegativeValue()
    {
        var item = CreateItem(initialQuantity: 10m);

        Assert.Throws<DomainValidationException>(() => item.Decrease(-1m, LaterUtc));
    }

    [Fact]
    public void DecreaseRejectsQuantityGreaterThanAvailable()
    {
        var item = CreateItem(initialQuantity: 5m);

        Assert.Throws<DomainValidationException>(() => item.Decrease(6m, LaterUtc));
    }

    [Fact]
    public void DecreaseRejectsNonUtcDate()
    {
        var item = CreateItem(initialQuantity: 10m);
        var nonUtc = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.FromHours(-5));

        Assert.Throws<DomainValidationException>(() => item.Decrease(4m, nonUtc));
    }

    [Fact]
    public void DecreaseRejectsDateBeforeUpdatedAtUtc()
    {
        var item = CreateItem(initialQuantity: 10m, createdAtUtc: LaterUtc);

        Assert.Throws<DomainValidationException>(() => item.Decrease(4m, CreatedAtUtc));
    }

    // ---------- Adjust ----------

    [Fact]
    public void AdjustIncreaseExecutesIncrement()
    {
        var item = CreateItem(initialQuantity: 10m);

        item.Adjust(InventoryAdjustmentType.Increase, 5m, LaterUtc);

        Assert.Equal(15m, item.Quantity);
        Assert.Equal(LaterUtc, item.UpdatedAtUtc);
    }

    [Fact]
    public void AdjustDecreaseExecutesDecrement()
    {
        var item = CreateItem(initialQuantity: 10m);

        item.Adjust(InventoryAdjustmentType.Decrease, 5m, LaterUtc);

        Assert.Equal(5m, item.Quantity);
        Assert.Equal(LaterUtc, item.UpdatedAtUtc);
    }

    [Fact]
    public void AdjustRejectsUndefinedEnumValue()
    {
        var item = CreateItem(initialQuantity: 10m);
        var undefined = (InventoryAdjustmentType)99;

        Assert.Throws<DomainValidationException>(() => item.Adjust(undefined, 5m, LaterUtc));
    }

    [Fact]
    public void AdjustIncreaseRejectsZeroQuantity()
    {
        var item = CreateItem(initialQuantity: 10m);

        Assert.Throws<DomainValidationException>(() => item.Adjust(InventoryAdjustmentType.Increase, 0m, LaterUtc));
    }

    [Fact]
    public void AdjustDecreaseRejectsQuantityGreaterThanAvailable()
    {
        var item = CreateItem(initialQuantity: 5m);

        Assert.Throws<DomainValidationException>(() => item.Adjust(InventoryAdjustmentType.Decrease, 6m, LaterUtc));
    }

    // ---------- ChangeReorderPoint ----------

    [Fact]
    public void ChangeReorderPointUpdatesValue()
    {
        var item = CreateItem(reorderPoint: 2m);

        item.ChangeReorderPoint(4m, LaterUtc);

        Assert.Equal(4m, item.ReorderPoint);
    }

    [Fact]
    public void ChangeReorderPointAllowsZero()
    {
        var item = CreateItem(reorderPoint: 2m);

        item.ChangeReorderPoint(0m, LaterUtc);

        Assert.Equal(0m, item.ReorderPoint);
    }

    [Fact]
    public void ChangeReorderPointAllowsDecimalValue()
    {
        var item = CreateItem(reorderPoint: 2m);

        item.ChangeReorderPoint(1.75m, LaterUtc);

        Assert.Equal(1.75m, item.ReorderPoint);
    }

    [Fact]
    public void ChangeReorderPointUpdatesUpdatedAtUtc()
    {
        var item = CreateItem(reorderPoint: 2m, createdAtUtc: CreatedAtUtc);

        item.ChangeReorderPoint(4m, LaterUtc);

        Assert.Equal(LaterUtc, item.UpdatedAtUtc);
    }

    [Fact]
    public void ChangeReorderPointAllowsDateEqualToUpdatedAtUtc()
    {
        var item = CreateItem(reorderPoint: 2m, createdAtUtc: CreatedAtUtc);

        item.ChangeReorderPoint(4m, CreatedAtUtc);

        Assert.Equal(CreatedAtUtc, item.UpdatedAtUtc);
    }

    [Fact]
    public void ChangeReorderPointRejectsNegativeValue()
    {
        var item = CreateItem();

        Assert.Throws<DomainValidationException>(() => item.ChangeReorderPoint(-1m, LaterUtc));
    }

    [Fact]
    public void ChangeReorderPointRejectsNonUtcDate()
    {
        var item = CreateItem();
        var nonUtc = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.FromHours(-5));

        Assert.Throws<DomainValidationException>(() => item.ChangeReorderPoint(4m, nonUtc));
    }

    [Fact]
    public void ChangeReorderPointRejectsDateBeforeUpdatedAtUtc()
    {
        var item = CreateItem(createdAtUtc: LaterUtc);

        Assert.Throws<DomainValidationException>(() => item.ChangeReorderPoint(4m, CreatedAtUtc));
    }

    // ---------- IsBelowReorderPoint ----------

    [Fact]
    public void IsBelowReorderPointReturnsTrueWhenQuantityIsLower()
    {
        var item = CreateItem(initialQuantity: 1m, reorderPoint: 2m);

        Assert.True(item.IsBelowReorderPoint());
    }

    [Fact]
    public void IsBelowReorderPointReturnsTrueWhenQuantityIsEqual()
    {
        var item = CreateItem(initialQuantity: 2m, reorderPoint: 2m);

        Assert.True(item.IsBelowReorderPoint());
    }

    [Fact]
    public void IsBelowReorderPointReturnsFalseWhenQuantityIsGreater()
    {
        var item = CreateItem(initialQuantity: 3m, reorderPoint: 2m);

        Assert.False(item.IsBelowReorderPoint());
    }

    // ---------- ApplyMovement ----------

    private static InventoryMovement CreateManualIncreaseMovement(
        InventoryItem item,
        decimal quantity = 5m,
        DateTimeOffset? occurredAtUtc = null,
        InventoryItemId? inventoryItemId = null,
        BranchId? branchId = null,
        ProductId? productId = null,
        decimal? quantityBefore = null) =>
        InventoryMovement.CreateManualIncrease(
            InventoryMovementId.New(),
            inventoryItemId ?? item.Id,
            branchId ?? item.BranchId,
            productId ?? item.ProductId,
            UserId.New(),
            quantity,
            quantityBefore ?? item.Quantity,
            occurredAtUtc ?? LaterUtc);

    private static InventoryMovement CreateManualDecreaseMovement(
        InventoryItem item,
        decimal quantity = 4m,
        DateTimeOffset? occurredAtUtc = null,
        decimal? quantityBefore = null) =>
        InventoryMovement.CreateManualDecrease(
            InventoryMovementId.New(),
            item.Id,
            item.BranchId,
            item.ProductId,
            UserId.New(),
            quantity,
            quantityBefore ?? item.Quantity,
            occurredAtUtc ?? LaterUtc);

    private static InventoryMovement CreateSaleDecreaseMovement(
        InventoryItem item,
        decimal quantity = 3m,
        DateTimeOffset? occurredAtUtc = null,
        decimal? quantityBefore = null) =>
        InventoryMovement.CreateSaleDecrease(
            InventoryMovementId.New(),
            item.Id,
            item.BranchId,
            item.ProductId,
            UserId.New(),
            SaleId.New(),
            SaleLineId.New(),
            quantity,
            quantityBefore ?? item.Quantity,
            occurredAtUtc ?? LaterUtc);

    [Fact]
    public void ApplyMovementAppliesManualIncrease()
    {
        var item = CreateItem(initialQuantity: 10m, createdAtUtc: CreatedAtUtc);
        var movement = CreateManualIncreaseMovement(item, quantity: 5m);

        item.ApplyMovement(movement);

        Assert.Equal(15m, item.Quantity);
    }

    [Fact]
    public void ApplyMovementAppliesManualDecrease()
    {
        var item = CreateItem(initialQuantity: 10m, createdAtUtc: CreatedAtUtc);
        var movement = CreateManualDecreaseMovement(item, quantity: 4m);

        item.ApplyMovement(movement);

        Assert.Equal(6m, item.Quantity);
    }

    [Fact]
    public void ApplyMovementAppliesSaleDecrease()
    {
        var item = CreateItem(initialQuantity: 10m, createdAtUtc: CreatedAtUtc);
        var movement = CreateSaleDecreaseMovement(item, quantity: 3m);

        item.ApplyMovement(movement);

        Assert.Equal(7m, item.Quantity);
    }

    [Fact]
    public void ApplyMovementUpdatesUpdatedAtUtc()
    {
        var item = CreateItem(initialQuantity: 10m, createdAtUtc: CreatedAtUtc);
        var movement = CreateManualIncreaseMovement(item, quantity: 5m, occurredAtUtc: LaterUtc);

        item.ApplyMovement(movement);

        Assert.Equal(LaterUtc, item.UpdatedAtUtc);
    }

    [Fact]
    public void ApplyMovementDoesNotModifyReorderPoint()
    {
        var item = CreateItem(initialQuantity: 10m, reorderPoint: 2m, createdAtUtc: CreatedAtUtc);
        var movement = CreateManualIncreaseMovement(item, quantity: 5m);

        item.ApplyMovement(movement);

        Assert.Equal(2m, item.ReorderPoint);
    }

    [Fact]
    public void ApplyMovementAllowsOccurredAtUtcEqualToUpdatedAtUtc()
    {
        var item = CreateItem(initialQuantity: 10m, createdAtUtc: CreatedAtUtc);
        var movement = CreateManualIncreaseMovement(item, quantity: 5m, occurredAtUtc: CreatedAtUtc);

        item.ApplyMovement(movement);

        Assert.Equal(CreatedAtUtc, item.UpdatedAtUtc);
    }

    [Fact]
    public void ApplyMovementAllowsDecimalQuantities()
    {
        var item = CreateItem(initialQuantity: 10.5m, createdAtUtc: CreatedAtUtc);
        var movement = CreateManualIncreaseMovement(item, quantity: 1.25m);

        item.ApplyMovement(movement);

        Assert.Equal(11.75m, item.Quantity);
    }

    [Fact]
    public void ApplyMovementRejectsNullMovement()
    {
        var item = CreateItem(initialQuantity: 10m, createdAtUtc: CreatedAtUtc);

        Assert.Throws<DomainValidationException>(() => item.ApplyMovement(null!));
    }

    [Fact]
    public void ApplyMovementRejectsMismatchedInventoryItemId()
    {
        var item = CreateItem(initialQuantity: 10m, createdAtUtc: CreatedAtUtc);
        var movement = CreateManualIncreaseMovement(item, quantity: 5m, inventoryItemId: InventoryItemId.New());

        Assert.Throws<DomainValidationException>(() => item.ApplyMovement(movement));
    }

    [Fact]
    public void ApplyMovementRejectsMismatchedBranchId()
    {
        var item = CreateItem(initialQuantity: 10m, createdAtUtc: CreatedAtUtc);
        var movement = CreateManualIncreaseMovement(item, quantity: 5m, branchId: BranchId.New());

        Assert.Throws<DomainValidationException>(() => item.ApplyMovement(movement));
    }

    [Fact]
    public void ApplyMovementRejectsMismatchedProductId()
    {
        var item = CreateItem(initialQuantity: 10m, createdAtUtc: CreatedAtUtc);
        var movement = CreateManualIncreaseMovement(item, quantity: 5m, productId: ProductId.New());

        Assert.Throws<DomainValidationException>(() => item.ApplyMovement(movement));
    }

    [Fact]
    public void ApplyMovementRejectsQuantityBeforeMismatch()
    {
        var item = CreateItem(initialQuantity: 10m, createdAtUtc: CreatedAtUtc);
        var movement = CreateManualIncreaseMovement(item, quantity: 5m, quantityBefore: 9m);

        Assert.Throws<DomainValidationException>(() => item.ApplyMovement(movement));
    }

    [Fact]
    public void ApplyMovementRejectsDateBeforeUpdatedAtUtc()
    {
        var item = CreateItem(initialQuantity: 10m, createdAtUtc: LaterUtc);
        var movement = CreateManualIncreaseMovement(item, quantity: 5m, occurredAtUtc: CreatedAtUtc);

        Assert.Throws<DomainValidationException>(() => item.ApplyMovement(movement));
    }

    [Fact]
    public void FailedApplyMovementDueToMismatchedInventoryItemIdDoesNotModifyState()
    {
        var item = CreateItem(initialQuantity: 10m, reorderPoint: 2m, createdAtUtc: CreatedAtUtc);
        var movement = CreateManualIncreaseMovement(item, quantity: 5m, inventoryItemId: InventoryItemId.New());

        Assert.Throws<DomainValidationException>(() => item.ApplyMovement(movement));

        Assert.Equal(10m, item.Quantity);
        Assert.Equal(2m, item.ReorderPoint);
        Assert.Equal(CreatedAtUtc, item.UpdatedAtUtc);
    }

    [Fact]
    public void FailedApplyMovementDueToQuantityBeforeMismatchDoesNotModifyState()
    {
        var item = CreateItem(initialQuantity: 10m, reorderPoint: 2m, createdAtUtc: CreatedAtUtc);
        var movement = CreateManualIncreaseMovement(item, quantity: 5m, quantityBefore: 9m);

        Assert.Throws<DomainValidationException>(() => item.ApplyMovement(movement));

        Assert.Equal(10m, item.Quantity);
        Assert.Equal(2m, item.ReorderPoint);
        Assert.Equal(CreatedAtUtc, item.UpdatedAtUtc);
    }

    [Fact]
    public void FailedApplyMovementDueToDateBeforeUpdatedAtUtcDoesNotModifyState()
    {
        var item = CreateItem(initialQuantity: 10m, reorderPoint: 2m, createdAtUtc: LaterUtc);
        var movement = CreateManualIncreaseMovement(item, quantity: 5m, occurredAtUtc: CreatedAtUtc);

        Assert.Throws<DomainValidationException>(() => item.ApplyMovement(movement));

        Assert.Equal(10m, item.Quantity);
        Assert.Equal(2m, item.ReorderPoint);
        Assert.Equal(LaterUtc, item.UpdatedAtUtc);
    }

    // ---------- Atomicidad ----------

    [Fact]
    public void FailedIncreaseDueToNonUtcDateDoesNotModifyState()
    {
        var item = CreateItem(initialQuantity: 10m, createdAtUtc: CreatedAtUtc);
        var nonUtc = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.FromHours(-5));

        Assert.Throws<DomainValidationException>(() => item.Increase(5m, nonUtc));

        Assert.Equal(10m, item.Quantity);
        Assert.Equal(CreatedAtUtc, item.UpdatedAtUtc);
    }

    [Fact]
    public void FailedDecreaseDueToInsufficientQuantityDoesNotModifyState()
    {
        var item = CreateItem(initialQuantity: 5m, createdAtUtc: CreatedAtUtc);

        Assert.Throws<DomainValidationException>(() => item.Decrease(6m, LaterUtc));

        Assert.Equal(5m, item.Quantity);
        Assert.Equal(CreatedAtUtc, item.UpdatedAtUtc);
    }

    [Fact]
    public void FailedDecreaseDueToDateBeforeUpdatedAtUtcDoesNotModifyState()
    {
        var item = CreateItem(initialQuantity: 10m, createdAtUtc: LaterUtc);

        Assert.Throws<DomainValidationException>(() => item.Decrease(4m, CreatedAtUtc));

        Assert.Equal(10m, item.Quantity);
        Assert.Equal(LaterUtc, item.UpdatedAtUtc);
    }

    [Fact]
    public void FailedChangeReorderPointDoesNotModifyState()
    {
        var item = CreateItem(reorderPoint: 2m, createdAtUtc: CreatedAtUtc);

        Assert.Throws<DomainValidationException>(() => item.ChangeReorderPoint(-1m, LaterUtc));

        Assert.Equal(2m, item.ReorderPoint);
        Assert.Equal(CreatedAtUtc, item.UpdatedAtUtc);
    }

    [Fact]
    public void FailedAdjustDueToUndefinedEnumDoesNotModifyState()
    {
        var item = CreateItem(initialQuantity: 10m, reorderPoint: 2m, createdAtUtc: CreatedAtUtc);
        var undefined = (InventoryAdjustmentType)99;

        Assert.Throws<DomainValidationException>(() => item.Adjust(undefined, 5m, LaterUtc));

        Assert.Equal(10m, item.Quantity);
        Assert.Equal(2m, item.ReorderPoint);
        Assert.Equal(CreatedAtUtc, item.UpdatedAtUtc);
    }
}
