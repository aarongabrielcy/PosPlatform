using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Inventory;
using Pos.Infrastructure.Persistence.Exceptions;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Persistence.Mappers;

internal static class InventoryItemMapper
{
    internal static InventoryItem ToDomain(InventoryItemRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        try
        {
            return InventoryItem.Rehydrate(
                new InventoryItemId(record.Id),
                new BranchId(record.BranchId),
                new ProductId(record.ProductId),
                record.Quantity,
                record.ReorderPoint,
                record.CreatedAtUtc,
                record.UpdatedAtUtc);
        }
        catch (DomainValidationException ex)
        {
            throw new PersistenceDataException(
                $"InventoryItemRecord con Id '{record.Id}' contiene datos inválidos: {ex.Message}", ex);
        }
    }

    internal static InventoryItemRecord ToRecord(InventoryItem inventoryItem)
    {
        ArgumentNullException.ThrowIfNull(inventoryItem);

        return new InventoryItemRecord
        {
            Id = inventoryItem.Id.Value,
            BranchId = inventoryItem.BranchId.Value,
            ProductId = inventoryItem.ProductId.Value,
            Quantity = inventoryItem.Quantity,
            ReorderPoint = inventoryItem.ReorderPoint,
            CreatedAtUtc = inventoryItem.CreatedAtUtc,
            UpdatedAtUtc = inventoryItem.UpdatedAtUtc,
        };
    }

    internal static void UpdateRecord(InventoryItem inventoryItem, InventoryItemRecord record)
    {
        ArgumentNullException.ThrowIfNull(inventoryItem);
        ArgumentNullException.ThrowIfNull(record);

        if (record.Id != inventoryItem.Id.Value)
        {
            throw new PersistenceDataException(
                $"No se puede actualizar InventoryItemRecord '{record.Id}': Id no coincide con InventoryItem '{inventoryItem.Id}'.");
        }

        if (record.BranchId != inventoryItem.BranchId.Value)
        {
            throw new PersistenceDataException(
                $"No se puede actualizar InventoryItemRecord '{record.Id}': BranchId no coincide.");
        }

        if (record.ProductId != inventoryItem.ProductId.Value)
        {
            throw new PersistenceDataException(
                $"No se puede actualizar InventoryItemRecord '{record.Id}': ProductId no coincide.");
        }

        if (record.CreatedAtUtc != inventoryItem.CreatedAtUtc)
        {
            throw new PersistenceDataException(
                $"No se puede actualizar InventoryItemRecord '{record.Id}': CreatedAtUtc no coincide.");
        }

        record.Quantity = inventoryItem.Quantity;
        record.ReorderPoint = inventoryItem.ReorderPoint;
        record.UpdatedAtUtc = inventoryItem.UpdatedAtUtc;
    }
}
