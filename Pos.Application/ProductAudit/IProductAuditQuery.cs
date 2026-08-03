using Pos.Domain.Common.Identifiers;

namespace Pos.Application.ProductAudit;

// Consulta especializada de solo lectura (TAREA 24D, sección 20/30), igual patrón que
// IProductCatalogQuery: vive en Application porque expone solo DTOs Application, pero su
// implementación cruza Records de Infrastructure. Ambos métodos son tenant-scoped por
// OrganizationId: nunca se consulta auditoría de otra Organization.
public interface IProductAuditQuery
{
    Task<ProductAuditPageResult> SearchPageAsync(
        OrganizationId organizationId,
        ProductAuditFilter filter,
        int skip,
        int take,
        CancellationToken cancellationToken);

    // Batch por lote de ProductId (TAREA 24D, sección 30): una sola consulta agrupada, nunca N
    // consultas por producto. sinceUtc acota "reciente" (24 horas en V1); los productos sin
    // actividad reciente simplemente no aparecen en el diccionario devuelto.
    Task<IReadOnlyDictionary<ProductId, ProductRecentActivity>> GetRecentActivityAsync(
        OrganizationId organizationId,
        IReadOnlyList<ProductId> productIds,
        DateTimeOffset sinceUtc,
        CancellationToken cancellationToken);
}
