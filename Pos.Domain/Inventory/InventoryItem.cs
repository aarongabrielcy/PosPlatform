using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;

namespace Pos.Domain.Inventory;

public sealed class InventoryItem
{
    public InventoryItemId Id { get; }

    public BranchId BranchId { get; }

    public ProductId ProductId { get; }

    public decimal Quantity { get; private set; }

    public decimal ReorderPoint { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public InventoryItem(
        InventoryItemId id,
        BranchId branchId,
        ProductId productId,
        decimal initialQuantity,
        decimal reorderPoint,
        DateTimeOffset createdAtUtc)
        : this(id, branchId, productId, initialQuantity, reorderPoint, createdAtUtc, createdAtUtc)
    {
    }

    private InventoryItem(
        InventoryItemId id,
        BranchId branchId,
        ProductId productId,
        decimal quantity,
        decimal reorderPoint,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        Id = EnsureNotEmpty(id);
        BranchId = EnsureNotEmpty(branchId);
        ProductId = EnsureNotEmpty(productId);
        Quantity = EnsureNonNegative(quantity, nameof(quantity));
        ReorderPoint = EnsureNonNegative(reorderPoint, nameof(reorderPoint));
        CreatedAtUtc = EnsureUtc(createdAtUtc, nameof(createdAtUtc));

        var validUpdatedAtUtc = EnsureUtc(updatedAtUtc, nameof(updatedAtUtc));

        if (validUpdatedAtUtc < CreatedAtUtc)
        {
            throw new DomainValidationException("updatedAtUtc no puede ser anterior a createdAtUtc.");
        }

        UpdatedAtUtc = validUpdatedAtUtc;
    }

    // Reconstruye estado ya persistido; a diferencia del constructor, permite UpdatedAtUtc posterior a CreatedAtUtc.
    public static InventoryItem Rehydrate(
        InventoryItemId id,
        BranchId branchId,
        ProductId productId,
        decimal quantity,
        decimal reorderPoint,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc) =>
        new(id, branchId, productId, quantity, reorderPoint, createdAtUtc, updatedAtUtc);

    public void Increase(decimal quantity, DateTimeOffset occurredAtUtc)
    {
        var validQuantity = EnsurePositive(quantity, nameof(quantity));
        var validOccurredAtUtc = EnsureUtc(occurredAtUtc, nameof(occurredAtUtc));
        EnsureNotBeforeUpdatedAt(validOccurredAtUtc, nameof(occurredAtUtc));

        Quantity += validQuantity;
        UpdatedAtUtc = validOccurredAtUtc;
    }

    public void Decrease(decimal quantity, DateTimeOffset occurredAtUtc)
    {
        var validQuantity = EnsurePositive(quantity, nameof(quantity));
        var validOccurredAtUtc = EnsureUtc(occurredAtUtc, nameof(occurredAtUtc));
        EnsureNotBeforeUpdatedAt(validOccurredAtUtc, nameof(occurredAtUtc));

        if (validQuantity > Quantity)
        {
            throw new DomainValidationException("No hay existencia suficiente para disminuir la cantidad solicitada.");
        }

        Quantity -= validQuantity;
        UpdatedAtUtc = validOccurredAtUtc;
    }

    public void Adjust(InventoryAdjustmentType adjustmentType, decimal quantity, DateTimeOffset occurredAtUtc)
    {
        switch (adjustmentType)
        {
            case InventoryAdjustmentType.Increase:
                Increase(quantity, occurredAtUtc);
                break;
            case InventoryAdjustmentType.Decrease:
                Decrease(quantity, occurredAtUtc);
                break;
            default:
                throw new DomainValidationException("adjustmentType no es un valor válido de InventoryAdjustmentType.");
        }
    }

    public void ApplyMovement(InventoryMovement movement)
    {
        if (movement is null)
        {
            throw new DomainValidationException("movement no puede ser nulo.");
        }

        if (movement.InventoryItemId != Id)
        {
            throw new DomainValidationException("movement.InventoryItemId debe coincidir con Id.");
        }

        if (movement.BranchId != BranchId)
        {
            throw new DomainValidationException("movement.BranchId debe coincidir con BranchId.");
        }

        if (movement.ProductId != ProductId)
        {
            throw new DomainValidationException("movement.ProductId debe coincidir con ProductId.");
        }

        if (movement.QuantityBefore != Quantity)
        {
            throw new DomainValidationException("movement.QuantityBefore debe coincidir con Quantity.");
        }

        var validOccurredAtUtc = EnsureUtc(movement.OccurredAtUtc, nameof(movement.OccurredAtUtc));
        EnsureNotBeforeUpdatedAt(validOccurredAtUtc, nameof(movement.OccurredAtUtc));

        if (movement.QuantityAfter < 0m)
        {
            throw new DomainValidationException("movement.QuantityAfter no puede ser negativa.");
        }

        Quantity = movement.QuantityAfter;
        UpdatedAtUtc = validOccurredAtUtc;
    }

    public void ChangeReorderPoint(decimal reorderPoint, DateTimeOffset changedAtUtc)
    {
        var validReorderPoint = EnsureNonNegative(reorderPoint, nameof(reorderPoint));
        var validChangedAtUtc = EnsureUtc(changedAtUtc, nameof(changedAtUtc));
        EnsureNotBeforeUpdatedAt(validChangedAtUtc, nameof(changedAtUtc));

        ReorderPoint = validReorderPoint;
        UpdatedAtUtc = validChangedAtUtc;
    }

    public bool IsBelowReorderPoint() => Quantity <= ReorderPoint;

    private void EnsureNotBeforeUpdatedAt(DateTimeOffset value, string parameterName)
    {
        if (value < UpdatedAtUtc)
        {
            throw new DomainValidationException($"{parameterName} no puede ser anterior a UpdatedAtUtc.");
        }
    }

    private static InventoryItemId EnsureNotEmpty(InventoryItemId id)
    {
        if (id.Value == Guid.Empty)
        {
            throw new DomainValidationException("Id no puede ser vacío.");
        }

        return id;
    }

    private static BranchId EnsureNotEmpty(BranchId branchId)
    {
        if (branchId.Value == Guid.Empty)
        {
            throw new DomainValidationException("BranchId no puede ser vacío.");
        }

        return branchId;
    }

    private static ProductId EnsureNotEmpty(ProductId productId)
    {
        if (productId.Value == Guid.Empty)
        {
            throw new DomainValidationException("ProductId no puede ser vacío.");
        }

        return productId;
    }

    private static decimal EnsureNonNegative(decimal value, string parameterName)
    {
        if (value < 0m)
        {
            throw new DomainValidationException($"{parameterName} no puede ser negativo.");
        }

        return value;
    }

    private static decimal EnsurePositive(decimal value, string parameterName)
    {
        if (value <= 0m)
        {
            throw new DomainValidationException($"{parameterName} debe ser mayor que cero.");
        }

        return value;
    }

    private static DateTimeOffset EnsureUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new DomainValidationException($"{parameterName} debe tener Offset igual a TimeSpan.Zero.");
        }

        return value;
    }
}
