using Pos.Domain.Common.Identifiers;

namespace Pos.Application.Inventory;

// Consulta especializada de solo lectura para el módulo operativo de Inventario (TAREA 24G):
// combina Product+InventoryItem (por Branch) en una sola consulta con filtro/orden/paginación
// aplicados en el proveedor de datos, evitando el N+1 de resolver la existencia producto por
// producto. Vive en Application porque expone solo tipos Application (DTOs), pero su
// implementación cruza Records de Infrastructure, igual criterio que IProductCatalogQuery.
public interface IInventoryCatalogQuery
{
    Task<InventoryCatalogPageResult> SearchPageAsync(
        OrganizationId organizationId,
        BranchId branchId,
        string? searchTerm,
        InventoryCatalogStatusFilter filter,
        int skip,
        int take,
        CancellationToken cancellationToken);

    Task<InventorySummary> GetSummaryAsync(
        OrganizationId organizationId,
        BranchId branchId,
        CancellationToken cancellationToken);
}
