namespace Pos.Application.Inventory;

// Consumido directamente por Pos.Desktop (Inventario, TAREA 24G): resuelve el usuario actual, el
// permiso (ManageProducts OR AdjustInventory) y la Organization/Branch actuales, y delega en
// IInventoryCatalogQuery/IInventoryMovementQuery. Desktop nunca manda OrganizationId/BranchId
// arbitrarios, igual criterio que IProductAuditService/ProductManagementService. El ajuste de
// existencia en sí NO se expone aquí: sigue viviendo únicamente en
// IProductManagementService.AdjustInventoryAsync (sección 16 de la tarea: no duplicar el flujo de
// escritura).
public interface IInventoryService
{
    Task<InventoryCatalogPageResult> GetCatalogPageAsync(
        string? searchTerm,
        InventoryCatalogStatusFilter filter,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<InventorySummary> GetSummaryAsync(CancellationToken cancellationToken = default);

    Task<InventoryMovementPageResult> GetMovementPageAsync(
        InventoryMovementFilter filter,
        int skip,
        int take,
        CancellationToken cancellationToken = default);
}
