using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;

namespace Pos.Domain.Inventory;

public sealed class InventoryMovement
{
    public InventoryMovementId Id { get; }

    public InventoryItemId InventoryItemId { get; }

    public BranchId BranchId { get; }

    public ProductId ProductId { get; }

    public UserId PerformedByUserId { get; }

    public InventoryMovementType Type { get; }

    public decimal Quantity { get; }

    public decimal QuantityBefore { get; }

    public decimal QuantityAfter { get; }

    public SaleId? SaleId { get; }

    public SaleLineId? SaleLineId { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    private InventoryMovement(
        InventoryMovementId id,
        InventoryItemId inventoryItemId,
        BranchId branchId,
        ProductId productId,
        UserId performedByUserId,
        InventoryMovementType type,
        decimal quantity,
        decimal quantityBefore,
        decimal quantityAfter,
        SaleId? saleId,
        SaleLineId? saleLineId,
        DateTimeOffset occurredAtUtc)
    {
        Id = EnsureNotEmpty(id);
        InventoryItemId = EnsureNotEmpty(inventoryItemId);
        BranchId = EnsureNotEmpty(branchId);
        ProductId = EnsureNotEmpty(productId);
        PerformedByUserId = EnsureNotEmpty(performedByUserId);
        Type = EnsureDefined(type);
        Quantity = EnsurePositive(quantity, nameof(quantity));
        QuantityBefore = EnsureNonNegative(quantityBefore, nameof(quantityBefore));
        QuantityAfter = EnsureNonNegative(quantityAfter, nameof(quantityAfter));
        OccurredAtUtc = EnsureUtc(occurredAtUtc, nameof(occurredAtUtc));

        EnsureTypeRules(Type, Quantity, QuantityBefore, QuantityAfter, saleId, saleLineId);

        SaleId = saleId;
        SaleLineId = saleLineId;
    }

    public static InventoryMovement CreateManualIncrease(
        InventoryMovementId id,
        InventoryItemId inventoryItemId,
        BranchId branchId,
        ProductId productId,
        UserId performedByUserId,
        decimal quantity,
        decimal quantityBefore,
        DateTimeOffset occurredAtUtc) =>
        new(
            id,
            inventoryItemId,
            branchId,
            productId,
            performedByUserId,
            InventoryMovementType.ManualIncrease,
            quantity,
            quantityBefore,
            quantityBefore + quantity,
            saleId: null,
            saleLineId: null,
            occurredAtUtc);

    public static InventoryMovement CreateManualDecrease(
        InventoryMovementId id,
        InventoryItemId inventoryItemId,
        BranchId branchId,
        ProductId productId,
        UserId performedByUserId,
        decimal quantity,
        decimal quantityBefore,
        DateTimeOffset occurredAtUtc) =>
        new(
            id,
            inventoryItemId,
            branchId,
            productId,
            performedByUserId,
            InventoryMovementType.ManualDecrease,
            quantity,
            quantityBefore,
            quantityBefore - quantity,
            saleId: null,
            saleLineId: null,
            occurredAtUtc);

    public static InventoryMovement CreateSaleDecrease(
        InventoryMovementId id,
        InventoryItemId inventoryItemId,
        BranchId branchId,
        ProductId productId,
        UserId performedByUserId,
        SaleId saleId,
        SaleLineId saleLineId,
        decimal quantity,
        decimal quantityBefore,
        DateTimeOffset occurredAtUtc) =>
        new(
            id,
            inventoryItemId,
            branchId,
            productId,
            performedByUserId,
            InventoryMovementType.SaleDecrease,
            quantity,
            quantityBefore,
            quantityBefore - quantity,
            saleId,
            saleLineId,
            occurredAtUtc);

    private static void EnsureTypeRules(
        InventoryMovementType type,
        decimal quantity,
        decimal quantityBefore,
        decimal quantityAfter,
        SaleId? saleId,
        SaleLineId? saleLineId)
    {
        switch (type)
        {
            case InventoryMovementType.ManualIncrease:
                EnsureNoSaleReference(saleId, saleLineId);
                EnsureExactQuantityAfter(quantityBefore + quantity, quantityAfter);
                break;
            case InventoryMovementType.ManualDecrease:
                EnsureNoSaleReference(saleId, saleLineId);
                EnsureQuantityNotGreaterThanBefore(quantity, quantityBefore);
                EnsureExactQuantityAfter(quantityBefore - quantity, quantityAfter);
                break;
            case InventoryMovementType.SaleDecrease:
                EnsureSaleReference(saleId, saleLineId);
                EnsureQuantityNotGreaterThanBefore(quantity, quantityBefore);
                EnsureExactQuantityAfter(quantityBefore - quantity, quantityAfter);
                break;
            default:
                throw new DomainValidationException("Type no es un valor válido de InventoryMovementType.");
        }
    }

    private static void EnsureNoSaleReference(SaleId? saleId, SaleLineId? saleLineId)
    {
        if (saleId is not null || saleLineId is not null)
        {
            throw new DomainValidationException(
                "ManualIncrease y ManualDecrease no pueden referenciar SaleId ni SaleLineId.");
        }
    }

    private static void EnsureSaleReference(SaleId? saleId, SaleLineId? saleLineId)
    {
        if (saleId is null || saleId.Value.Value == Guid.Empty)
        {
            throw new DomainValidationException("SaleId es obligatorio para SaleDecrease.");
        }

        if (saleLineId is null || saleLineId.Value.Value == Guid.Empty)
        {
            throw new DomainValidationException("SaleLineId es obligatorio para SaleDecrease.");
        }
    }

    private static void EnsureQuantityNotGreaterThanBefore(decimal quantity, decimal quantityBefore)
    {
        if (quantity > quantityBefore)
        {
            throw new DomainValidationException("Quantity no puede ser mayor que QuantityBefore.");
        }
    }

    private static void EnsureExactQuantityAfter(decimal expected, decimal actual)
    {
        if (expected != actual)
        {
            throw new DomainValidationException("QuantityAfter no coincide con el cálculo esperado para Type.");
        }
    }

    private static InventoryMovementId EnsureNotEmpty(InventoryMovementId id)
    {
        if (id.Value == Guid.Empty)
        {
            throw new DomainValidationException("Id no puede ser vacío.");
        }

        return id;
    }

    private static InventoryItemId EnsureNotEmpty(InventoryItemId inventoryItemId)
    {
        if (inventoryItemId.Value == Guid.Empty)
        {
            throw new DomainValidationException("InventoryItemId no puede ser vacío.");
        }

        return inventoryItemId;
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

    private static UserId EnsureNotEmpty(UserId userId)
    {
        if (userId.Value == Guid.Empty)
        {
            throw new DomainValidationException("PerformedByUserId no puede ser vacío.");
        }

        return userId;
    }

    private static InventoryMovementType EnsureDefined(InventoryMovementType type)
    {
        if (!Enum.IsDefined(type))
        {
            throw new DomainValidationException("Type no es un valor válido de InventoryMovementType.");
        }

        return type;
    }

    private static decimal EnsurePositive(decimal value, string parameterName)
    {
        if (value <= 0m)
        {
            throw new DomainValidationException($"{parameterName} debe ser mayor que cero.");
        }

        return value;
    }

    private static decimal EnsureNonNegative(decimal value, string parameterName)
    {
        if (value < 0m)
        {
            throw new DomainValidationException($"{parameterName} no puede ser negativo.");
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
