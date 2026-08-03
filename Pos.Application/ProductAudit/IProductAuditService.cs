namespace Pos.Application.ProductAudit;

// Consumido directamente por Pos.Desktop (Auditoría > Productos): resuelve el usuario actual, el
// permiso ViewProductAudit y la OrganizationId, y delega en IProductAuditQuery. Desktop nunca
// manda OrganizationId (TAREA 24D, sección 15/22).
public interface IProductAuditService
{
    Task<ProductAuditPageResult> SearchPageAsync(
        ProductAuditFilter filter, int skip, int take, CancellationToken cancellationToken = default);
}
