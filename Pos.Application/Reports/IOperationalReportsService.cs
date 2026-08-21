using Pos.Application.Inventory;
using Pos.Domain.Common.Identifiers;

namespace Pos.Application.Reports;

// Fachada de aplicación de los reportes operativos (BASIC-RPT-01): aplica ViewReports y el
// OrganizationId del usuario actual antes de delegar en IOperationalReportsQuery, mismo patrón que
// ISalesHistoryService. Manager/Administrator tienen ViewReports (StandardRoles/
// AdministrativePermissionSet); Cashier nunca lo tiene (sección 6/29 de la tarea).
public interface IOperationalReportsService
{
    Task<SalesSummaryReport> GetSalesSummaryAsync(
        DateTimeOffset fromUtc, DateTimeOffset toUtcExclusive, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RegisterClosureReportItem>> GetRegisterClosuresAsync(
        DateTimeOffset fromUtc, DateTimeOffset toUtcExclusive, CancellationToken cancellationToken = default);

    Task<RegisterClosureReportItem?> GetRegisterClosureDetailAsync(
        RegisterSessionId registerSessionId, CancellationToken cancellationToken = default);

    Task<CashMovementsReportResult> GetCashMovementsAsync(
        DateTimeOffset fromUtc, DateTimeOffset toUtcExclusive, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductSalesReportItem>> GetProductSalesAsync(
        DateTimeOffset fromUtc, DateTimeOffset toUtcExclusive, CancellationToken cancellationToken = default);

    // Estado actual del inventario (sección 16 de la tarea: reporte de estado, no histórico), sin
    // rango de fechas. Reutiliza exactamente la misma semántica de LowStock que el módulo
    // Inventario (IInventoryCatalogQuery), nunca una segunda definición de umbral.
    Task<IReadOnlyList<InventoryCatalogItem>> GetLowStockAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OperatorActivityReportItem>> GetOperatorActivityAsync(
        DateTimeOffset fromUtc, DateTimeOffset toUtcExclusive, CancellationToken cancellationToken = default);
}
