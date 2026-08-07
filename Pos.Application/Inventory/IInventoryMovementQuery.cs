using Pos.Domain.Common.Identifiers;

namespace Pos.Application.Inventory;

// Consulta especializada de solo lectura para el historial de movimientos (TAREA 24G, sección 21):
// join InventoryMovement+Product (por Branch+Organization) para resolver Sku/ProductName sin N+1.
// Read-only: no existe operación de escritura equivalente (los movimientos se crean únicamente a
// través de ProductManagementService.AdjustInventoryAsync / CheckoutService).
public interface IInventoryMovementQuery
{
    Task<InventoryMovementPageResult> SearchPageAsync(
        OrganizationId organizationId,
        BranchId branchId,
        InventoryMovementFilter filter,
        int skip,
        int take,
        CancellationToken cancellationToken);
}
