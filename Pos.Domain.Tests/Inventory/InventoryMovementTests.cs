using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Inventory;

namespace Pos.Domain.Tests.Inventory;

public class InventoryMovementTests
{
    private static readonly DateTimeOffset OccurredAtUtc = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    // ---------- CreateManualIncrease ----------

    [Fact]
    public void CreateManualIncreaseKeepsIdentifiers()
    {
        var id = InventoryMovementId.New();
        var inventoryItemId = InventoryItemId.New();
        var branchId = BranchId.New();
        var productId = ProductId.New();
        var userId = UserId.New();

        var movement = InventoryMovement.CreateManualIncrease(
            id, inventoryItemId, branchId, productId, userId, 5m, 10m, OccurredAtUtc);

        Assert.Equal(id, movement.Id);
        Assert.Equal(inventoryItemId, movement.InventoryItemId);
        Assert.Equal(branchId, movement.BranchId);
        Assert.Equal(productId, movement.ProductId);
    }

    [Fact]
    public void CreateManualIncreaseKeepsPerformedByUserId()
    {
        var userId = UserId.New();

        var movement = InventoryMovement.CreateManualIncrease(
            InventoryMovementId.New(), InventoryItemId.New(), BranchId.New(), ProductId.New(),
            userId, 5m, 10m, OccurredAtUtc);

        Assert.Equal(userId, movement.PerformedByUserId);
    }

    [Fact]
    public void CreateManualIncreaseSetsTypeManualIncrease()
    {
        var movement = CreateManualIncrease(quantity: 5m, quantityBefore: 10m);

        Assert.Equal(InventoryMovementType.ManualIncrease, movement.Type);
    }

    [Fact]
    public void CreateManualIncreaseKeepsQuantityAndQuantityBefore()
    {
        var movement = CreateManualIncrease(quantity: 5m, quantityBefore: 10m);

        Assert.Equal(5m, movement.Quantity);
        Assert.Equal(10m, movement.QuantityBefore);
    }

    [Fact]
    public void CreateManualIncreaseCalculatesQuantityAfter()
    {
        var movement = CreateManualIncrease(quantity: 5m, quantityBefore: 10m);

        Assert.Equal(15m, movement.QuantityAfter);
    }

    [Fact]
    public void CreateManualIncreaseLeavesSaleReferencesNull()
    {
        var movement = CreateManualIncrease(quantity: 5m, quantityBefore: 10m);

        Assert.Null(movement.SaleId);
        Assert.Null(movement.SaleLineId);
    }

    [Fact]
    public void CreateManualIncreaseAllowsDecimalQuantities()
    {
        var movement = CreateManualIncrease(quantity: 1.25m, quantityBefore: 10.5m);

        Assert.Equal(11.75m, movement.QuantityAfter);
    }

    // ---------- CreateManualDecrease ----------

    [Fact]
    public void CreateManualDecreaseSetsTypeManualDecrease()
    {
        var movement = CreateManualDecrease(quantity: 4m, quantityBefore: 10m);

        Assert.Equal(InventoryMovementType.ManualDecrease, movement.Type);
    }

    [Fact]
    public void CreateManualDecreaseCalculatesQuantityAfter()
    {
        var movement = CreateManualDecrease(quantity: 4m, quantityBefore: 10m);

        Assert.Equal(6m, movement.QuantityAfter);
    }

    [Fact]
    public void CreateManualDecreaseAllowsQuantityAfterToReachZero()
    {
        var movement = CreateManualDecrease(quantity: 10m, quantityBefore: 10m);

        Assert.Equal(0m, movement.QuantityAfter);
    }

    [Fact]
    public void CreateManualDecreaseAllowsDecimalQuantities()
    {
        var movement = CreateManualDecrease(quantity: 1.5m, quantityBefore: 10m);

        Assert.Equal(8.5m, movement.QuantityAfter);
    }

    [Fact]
    public void CreateManualDecreaseLeavesSaleReferencesNull()
    {
        var movement = CreateManualDecrease(quantity: 4m, quantityBefore: 10m);

        Assert.Null(movement.SaleId);
        Assert.Null(movement.SaleLineId);
    }

    [Fact]
    public void CreateManualDecreaseRejectsQuantityGreaterThanQuantityBefore()
    {
        Assert.Throws<DomainValidationException>(
            () => CreateManualDecrease(quantity: 11m, quantityBefore: 10m));
    }

    // ---------- CreateSaleDecrease ----------

    [Fact]
    public void CreateSaleDecreaseSetsTypeSaleDecrease()
    {
        var movement = CreateSaleDecrease(quantity: 3m, quantityBefore: 10m);

        Assert.Equal(InventoryMovementType.SaleDecrease, movement.Type);
    }

    [Fact]
    public void CreateSaleDecreaseKeepsSaleId()
    {
        var saleId = SaleId.New();

        var movement = CreateSaleDecrease(quantity: 3m, quantityBefore: 10m, saleId: saleId);

        Assert.Equal(saleId, movement.SaleId);
    }

    [Fact]
    public void CreateSaleDecreaseKeepsSaleLineId()
    {
        var saleLineId = SaleLineId.New();

        var movement = CreateSaleDecrease(quantity: 3m, quantityBefore: 10m, saleLineId: saleLineId);

        Assert.Equal(saleLineId, movement.SaleLineId);
    }

    [Fact]
    public void CreateSaleDecreaseCalculatesQuantityAfter()
    {
        var movement = CreateSaleDecrease(quantity: 3m, quantityBefore: 10m);

        Assert.Equal(7m, movement.QuantityAfter);
    }

    [Fact]
    public void CreateSaleDecreaseAllowsQuantityAfterToReachZero()
    {
        var movement = CreateSaleDecrease(quantity: 10m, quantityBefore: 10m);

        Assert.Equal(0m, movement.QuantityAfter);
    }

    [Fact]
    public void CreateSaleDecreaseAllowsDecimalQuantities()
    {
        var movement = CreateSaleDecrease(quantity: 1.5m, quantityBefore: 10m);

        Assert.Equal(8.5m, movement.QuantityAfter);
    }

    [Fact]
    public void CreateSaleDecreaseRejectsDefaultSaleId()
    {
        Assert.Throws<DomainValidationException>(
            () => CreateSaleDecrease(quantity: 3m, quantityBefore: 10m, saleId: default(SaleId)));
    }

    [Fact]
    public void CreateSaleDecreaseRejectsDefaultSaleLineId()
    {
        Assert.Throws<DomainValidationException>(
            () => CreateSaleDecrease(quantity: 3m, quantityBefore: 10m, saleLineId: default(SaleLineId)));
    }

    [Fact]
    public void CreateSaleDecreaseRejectsQuantityGreaterThanQuantityBefore()
    {
        Assert.Throws<DomainValidationException>(
            () => CreateSaleDecrease(quantity: 11m, quantityBefore: 10m));
    }

    // ---------- Validaciones generales ----------

    [Fact]
    public void CreateManualIncreaseRejectsDefaultInventoryMovementId()
    {
        Assert.Throws<DomainValidationException>(() => InventoryMovement.CreateManualIncrease(
            default, InventoryItemId.New(), BranchId.New(), ProductId.New(), UserId.New(),
            5m, 10m, OccurredAtUtc));
    }

    [Fact]
    public void CreateManualIncreaseRejectsDefaultInventoryItemId()
    {
        Assert.Throws<DomainValidationException>(() => InventoryMovement.CreateManualIncrease(
            InventoryMovementId.New(), default, BranchId.New(), ProductId.New(), UserId.New(),
            5m, 10m, OccurredAtUtc));
    }

    [Fact]
    public void CreateManualIncreaseRejectsDefaultBranchId()
    {
        Assert.Throws<DomainValidationException>(() => InventoryMovement.CreateManualIncrease(
            InventoryMovementId.New(), InventoryItemId.New(), default, ProductId.New(), UserId.New(),
            5m, 10m, OccurredAtUtc));
    }

    [Fact]
    public void CreateManualIncreaseRejectsDefaultProductId()
    {
        Assert.Throws<DomainValidationException>(() => InventoryMovement.CreateManualIncrease(
            InventoryMovementId.New(), InventoryItemId.New(), BranchId.New(), default, UserId.New(),
            5m, 10m, OccurredAtUtc));
    }

    [Fact]
    public void CreateManualIncreaseRejectsDefaultUserId()
    {
        Assert.Throws<DomainValidationException>(() => InventoryMovement.CreateManualIncrease(
            InventoryMovementId.New(), InventoryItemId.New(), BranchId.New(), ProductId.New(), default,
            5m, 10m, OccurredAtUtc));
    }

    [Fact]
    public void CreateManualIncreaseRejectsZeroQuantity()
    {
        Assert.Throws<DomainValidationException>(
            () => CreateManualIncrease(quantity: 0m, quantityBefore: 10m));
    }

    [Fact]
    public void CreateManualIncreaseRejectsNegativeQuantity()
    {
        Assert.Throws<DomainValidationException>(
            () => CreateManualIncrease(quantity: -1m, quantityBefore: 10m));
    }

    [Fact]
    public void CreateManualIncreaseRejectsNegativeQuantityBefore()
    {
        Assert.Throws<DomainValidationException>(
            () => CreateManualIncrease(quantity: 5m, quantityBefore: -1m));
    }

    [Fact]
    public void CreateManualIncreaseRejectsNonUtcOccurredAtUtc()
    {
        var nonUtc = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.FromHours(-5));

        Assert.Throws<DomainValidationException>(() => InventoryMovement.CreateManualIncrease(
            InventoryMovementId.New(), InventoryItemId.New(), BranchId.New(), ProductId.New(), UserId.New(),
            5m, 10m, nonUtc));
    }

    // ---------- Helpers ----------

    private static InventoryMovement CreateManualIncrease(decimal quantity, decimal quantityBefore) =>
        InventoryMovement.CreateManualIncrease(
            InventoryMovementId.New(),
            InventoryItemId.New(),
            BranchId.New(),
            ProductId.New(),
            UserId.New(),
            quantity,
            quantityBefore,
            OccurredAtUtc);

    private static InventoryMovement CreateManualDecrease(decimal quantity, decimal quantityBefore) =>
        InventoryMovement.CreateManualDecrease(
            InventoryMovementId.New(),
            InventoryItemId.New(),
            BranchId.New(),
            ProductId.New(),
            UserId.New(),
            quantity,
            quantityBefore,
            OccurredAtUtc);

    private static InventoryMovement CreateSaleDecrease(
        decimal quantity,
        decimal quantityBefore,
        SaleId? saleId = null,
        SaleLineId? saleLineId = null) =>
        InventoryMovement.CreateSaleDecrease(
            InventoryMovementId.New(),
            InventoryItemId.New(),
            BranchId.New(),
            ProductId.New(),
            UserId.New(),
            saleId ?? SaleId.New(),
            saleLineId ?? SaleLineId.New(),
            quantity,
            quantityBefore,
            OccurredAtUtc);
}
