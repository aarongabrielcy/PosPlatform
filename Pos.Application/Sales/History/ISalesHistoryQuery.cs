using Pos.Domain.Common.Identifiers;

namespace Pos.Application.Sales.History;

// Consulta especializada de solo lectura (TAREA 25B, sección 5/14), mismo patrón que
// IProductAuditQuery/IInventoryMovementQuery: vive en Application porque expone solo DTOs
// Application, pero su implementación (Infrastructure) cruza sales/sale_lines/payments/users/
// registers/register_sessions sin N+1. Todos los métodos son tenant-scoped por OrganizationId:
// nunca se consulta ni resuelve una venta de otra Organization (sección 28/29).
public interface ISalesHistoryQuery
{
    Task<SalesHistoryPageResult> SearchPageAsync(
        OrganizationId organizationId,
        SalesHistoryFilter filter,
        int skip,
        int take,
        CancellationToken cancellationToken);

    // Agregado independiente de la página cargada (TAREA 25B, sección 17): respeta los mismos
    // filtros que SearchPageAsync, pero nunca suma solo los "take" elementos de una página.
    Task<SalesHistorySummary> GetSummaryAsync(
        OrganizationId organizationId,
        SalesHistoryFilter filter,
        CancellationToken cancellationToken);

    // organizationId se aplica como filtro de la propia consulta (no solo verificado después de
    // cargar), de forma que un SaleId de otra Organization jamás se materializa (TAREA 25B, sección
    // 28): null cubre tanto "no existe" como "existe pero es de otra Organization".
    Task<SaleHistoryDetail?> GetDetailAsync(
        OrganizationId organizationId,
        SaleId saleId,
        CancellationToken cancellationToken);

    Task<SalesHistoryFilterOptions> GetFilterOptionsAsync(
        OrganizationId organizationId,
        CancellationToken cancellationToken);
}
