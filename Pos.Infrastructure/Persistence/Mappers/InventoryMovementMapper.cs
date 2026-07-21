using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Inventory;
using Pos.Infrastructure.Persistence.Exceptions;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Persistence.Mappers;

internal static class InventoryMovementMapper
{
    internal static InventoryMovementRecord ToRecord(InventoryMovement movement)
    {
        ArgumentNullException.ThrowIfNull(movement);

        return new InventoryMovementRecord
        {
            Id = movement.Id.Value,
            InventoryItemId = movement.InventoryItemId.Value,
            BranchId = movement.BranchId.Value,
            ProductId = movement.ProductId.Value,
            PerformedByUserId = movement.PerformedByUserId.Value,
            Type = movement.Type,
            Quantity = movement.Quantity,
            QuantityBefore = movement.QuantityBefore,
            QuantityAfter = movement.QuantityAfter,
            SaleId = movement.SaleId?.Value,
            SaleLineId = movement.SaleLineId?.Value,
            OccurredAtUtc = movement.OccurredAtUtc,
        };
    }

    internal static InventoryMovement ToDomain(InventoryMovementRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        InventoryMovement movement;

        try
        {
            movement = record.Type switch
            {
                InventoryMovementType.ManualIncrease => CreateManualIncrease(record),
                InventoryMovementType.ManualDecrease => CreateManualDecrease(record),
                InventoryMovementType.SaleDecrease => CreateSaleDecrease(record),
                _ => throw new PersistenceDataException(
                    $"InventoryMovementRecord con Id '{record.Id}' tiene un Type no soportado: '{record.Type}'."),
            };
        }
        catch (DomainValidationException ex)
        {
            throw new PersistenceDataException(
                $"InventoryMovementRecord con Id '{record.Id}' contiene datos inválidos: {ex.Message}", ex);
        }

        EnsureMatchesRecord(record, movement);

        return movement;
    }

    private static InventoryMovement CreateManualIncrease(InventoryMovementRecord record)
    {
        EnsureNoSaleReferences(record);

        return InventoryMovement.CreateManualIncrease(
            new InventoryMovementId(record.Id),
            new InventoryItemId(record.InventoryItemId),
            new BranchId(record.BranchId),
            new ProductId(record.ProductId),
            new UserId(record.PerformedByUserId),
            record.Quantity,
            record.QuantityBefore,
            record.OccurredAtUtc);
    }

    private static InventoryMovement CreateManualDecrease(InventoryMovementRecord record)
    {
        EnsureNoSaleReferences(record);

        return InventoryMovement.CreateManualDecrease(
            new InventoryMovementId(record.Id),
            new InventoryItemId(record.InventoryItemId),
            new BranchId(record.BranchId),
            new ProductId(record.ProductId),
            new UserId(record.PerformedByUserId),
            record.Quantity,
            record.QuantityBefore,
            record.OccurredAtUtc);
    }

    private static InventoryMovement CreateSaleDecrease(InventoryMovementRecord record)
    {
        if (record.SaleId is null)
        {
            throw new PersistenceDataException(
                $"InventoryMovementRecord con Id '{record.Id}' de tipo SaleDecrease requiere SaleId.");
        }

        if (record.SaleLineId is null)
        {
            throw new PersistenceDataException(
                $"InventoryMovementRecord con Id '{record.Id}' de tipo SaleDecrease requiere SaleLineId.");
        }

        return InventoryMovement.CreateSaleDecrease(
            new InventoryMovementId(record.Id),
            new InventoryItemId(record.InventoryItemId),
            new BranchId(record.BranchId),
            new ProductId(record.ProductId),
            new UserId(record.PerformedByUserId),
            new SaleId(record.SaleId.Value),
            new SaleLineId(record.SaleLineId.Value),
            record.Quantity,
            record.QuantityBefore,
            record.OccurredAtUtc);
    }

    private static void EnsureNoSaleReferences(InventoryMovementRecord record)
    {
        if (record.SaleId is not null || record.SaleLineId is not null)
        {
            throw new PersistenceDataException(
                $"InventoryMovementRecord con Id '{record.Id}' de tipo '{record.Type}' no debe tener SaleId ni SaleLineId.");
        }
    }

    private static void EnsureMatchesRecord(InventoryMovementRecord record, InventoryMovement movement)
    {
        if (movement.Id.Value != record.Id
            || movement.InventoryItemId.Value != record.InventoryItemId
            || movement.BranchId.Value != record.BranchId
            || movement.ProductId.Value != record.ProductId
            || movement.PerformedByUserId.Value != record.PerformedByUserId
            || movement.Type != record.Type
            || movement.Quantity != record.Quantity
            || movement.QuantityBefore != record.QuantityBefore
            || movement.QuantityAfter != record.QuantityAfter
            || movement.SaleId?.Value != record.SaleId
            || movement.SaleLineId?.Value != record.SaleLineId
            || movement.OccurredAtUtc != record.OccurredAtUtc)
        {
            throw new PersistenceDataException(
                $"InventoryMovementRecord con Id '{record.Id}' no coincide con el movimiento reconstruido a partir de sus datos.");
        }
    }
}
