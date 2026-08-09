using Pos.Domain.Common.Identifiers;

namespace Pos.Application.Sales.History;

// Consumido directamente por Pos.Desktop (Ventas > Historial): resuelve el usuario actual, el
// permiso ViewReports y la OrganizationId, y delega en ISalesHistoryQuery. Desktop nunca manda
// OrganizationId (TAREA 25B, sección 15/29). Read-only: no expone Edit/Delete/Cancel/Return
// (sección 50).
public interface ISalesHistoryService
{
    Task<SalesHistoryPageResult> SearchPageAsync(
        SalesHistoryFilter filter, int skip, int take, CancellationToken cancellationToken = default);

    Task<SalesHistorySummary> GetSummaryAsync(
        SalesHistoryFilter filter, CancellationToken cancellationToken = default);

    // Carga la venta exacta por SaleId (TAREA 25B, sección 32): nunca "última venta del cajero" ni
    // otra heurística. Null si no existe, el usuario no tiene permiso, o la venta pertenece a otra
    // Organization (sección 28).
    Task<SaleHistoryDetail?> GetDetailAsync(SaleId saleId, CancellationToken cancellationToken = default);

    Task<SalesHistoryFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken = default);
}
