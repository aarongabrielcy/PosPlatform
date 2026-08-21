using Pos.Domain.Common.Identifiers;

namespace Pos.Application.Reports;

// Consulta especializada de solo lectura para los reportes operativos (BASIC-RPT-01), mismo patrón
// que ISalesHistoryQuery/IInventoryCatalogQuery: vive en Application porque expone solo DTOs
// Application, pero su implementación (Infrastructure) cruza Sale/SaleLine/Payment/RegisterSession/
// CashMovement/Register/User sin N+1. Todos los métodos son tenant-scoped por OrganizationId
// (sección 22 de la tarea: nunca agregar datos de otra Organization). ToUtcExclusive es siempre un
// límite EXCLUSIVO (mismo criterio que SalesHistoryFilter): el llamador ya resolvió el día local
// antes de construir el rango. Low Stock no vive aquí: se sirve reutilizando IInventoryCatalogQuery
// directamente desde OperationalReportsService (sección 3/16 de la tarea: no duplicar consultas
// existentes).
public interface IOperationalReportsQuery
{
    Task<SalesSummaryReport> GetSalesSummaryAsync(
        OrganizationId organizationId, DateTimeOffset fromUtc, DateTimeOffset toUtcExclusive, CancellationToken cancellationToken);

    Task<IReadOnlyList<RegisterClosureReportItem>> GetRegisterClosuresAsync(
        OrganizationId organizationId, DateTimeOffset fromUtc, DateTimeOffset toUtcExclusive, CancellationToken cancellationToken);

    Task<RegisterClosureReportItem?> GetRegisterClosureDetailAsync(
        OrganizationId organizationId, RegisterSessionId registerSessionId, CancellationToken cancellationToken);

    Task<CashMovementsReportResult> GetCashMovementsAsync(
        OrganizationId organizationId, DateTimeOffset fromUtc, DateTimeOffset toUtcExclusive, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProductSalesReportItem>> GetProductSalesAsync(
        OrganizationId organizationId, DateTimeOffset fromUtc, DateTimeOffset toUtcExclusive, CancellationToken cancellationToken);

    Task<IReadOnlyList<OperatorActivityReportItem>> GetOperatorActivityAsync(
        OrganizationId organizationId, DateTimeOffset fromUtc, DateTimeOffset toUtcExclusive, CancellationToken cancellationToken);
}
