using Pos.Application.Authentication;
using Pos.Application.Branches;
using Pos.Application.Inventory;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;

namespace Pos.Application.Reports;

public sealed class OperationalReportsService : IOperationalReportsService
{
    // Límite práctico para reportes de estado actual sin paginación (Low Stock, sección 16 de la
    // tarea: no requiere paginación como el módulo Inventario, es una lista de estado, no un
    // histórico). Volúmenes de Basic V1 (una tienda, SQLite local) nunca se acercan a este límite.
    private const int LowStockTake = 1000;

    private readonly ICurrentUserSession _currentUserSession;
    private readonly IOperationalReportsQuery _reportsQuery;
    private readonly IInventoryCatalogQuery _inventoryCatalogQuery;
    private readonly IBranchRepository _branchRepository;

    public OperationalReportsService(
        ICurrentUserSession currentUserSession,
        IOperationalReportsQuery reportsQuery,
        IInventoryCatalogQuery inventoryCatalogQuery,
        IBranchRepository branchRepository)
    {
        _currentUserSession = currentUserSession ?? throw new ArgumentNullException(nameof(currentUserSession));
        _reportsQuery = reportsQuery ?? throw new ArgumentNullException(nameof(reportsQuery));
        _inventoryCatalogQuery = inventoryCatalogQuery ?? throw new ArgumentNullException(nameof(inventoryCatalogQuery));
        _branchRepository = branchRepository ?? throw new ArgumentNullException(nameof(branchRepository));
    }

    public async Task<SalesSummaryReport> GetSalesSummaryAsync(
        DateTimeOffset fromUtc, DateTimeOffset toUtcExclusive, CancellationToken cancellationToken = default)
    {
        if (!TryGetAuthorizedOrganization(out var organizationId) || fromUtc >= toUtcExclusive)
        {
            return SalesSummaryReport.Empty;
        }

        return await _reportsQuery.GetSalesSummaryAsync(organizationId, fromUtc, toUtcExclusive, cancellationToken);
    }

    public async Task<IReadOnlyList<RegisterClosureReportItem>> GetRegisterClosuresAsync(
        DateTimeOffset fromUtc, DateTimeOffset toUtcExclusive, CancellationToken cancellationToken = default)
    {
        if (!TryGetAuthorizedOrganization(out var organizationId) || fromUtc >= toUtcExclusive)
        {
            return Array.Empty<RegisterClosureReportItem>();
        }

        return await _reportsQuery.GetRegisterClosuresAsync(organizationId, fromUtc, toUtcExclusive, cancellationToken);
    }

    public async Task<RegisterClosureReportItem?> GetRegisterClosureDetailAsync(
        RegisterSessionId registerSessionId, CancellationToken cancellationToken = default)
    {
        if (!TryGetAuthorizedOrganization(out var organizationId))
        {
            return null;
        }

        return await _reportsQuery.GetRegisterClosureDetailAsync(organizationId, registerSessionId, cancellationToken);
    }

    public async Task<CashMovementsReportResult> GetCashMovementsAsync(
        DateTimeOffset fromUtc, DateTimeOffset toUtcExclusive, CancellationToken cancellationToken = default)
    {
        if (!TryGetAuthorizedOrganization(out var organizationId) || fromUtc >= toUtcExclusive)
        {
            return CashMovementsReportResult.Empty;
        }

        return await _reportsQuery.GetCashMovementsAsync(organizationId, fromUtc, toUtcExclusive, cancellationToken);
    }

    public async Task<IReadOnlyList<ProductSalesReportItem>> GetProductSalesAsync(
        DateTimeOffset fromUtc, DateTimeOffset toUtcExclusive, CancellationToken cancellationToken = default)
    {
        if (!TryGetAuthorizedOrganization(out var organizationId) || fromUtc >= toUtcExclusive)
        {
            return Array.Empty<ProductSalesReportItem>();
        }

        return await _reportsQuery.GetProductSalesAsync(organizationId, fromUtc, toUtcExclusive, cancellationToken);
    }

    // Sin rango de fechas (sección 16 de la tarea): estado actual del inventario, igual que el
    // módulo Inventario. La Branch se resuelve igual que RegisterSessionService.
    // IsSingleValidOrganizationAsync (única Branch activa de la única Organization instalada),
    // en vez de depender de ICurrentRegisterSession.Current (InventoryService sí depende de una
    // caja abierta, pero un Manager debe poder consultar Reportes sin tener una caja abierta).
    public async Task<IReadOnlyList<InventoryCatalogItem>> GetLowStockAsync(CancellationToken cancellationToken = default)
    {
        if (!TryGetAuthorizedOrganization(out var organizationId))
        {
            return Array.Empty<InventoryCatalogItem>();
        }

        var branches = await _branchRepository.GetByOrganizationAsync(organizationId, cancellationToken);
        var activeBranch = branches.FirstOrDefault(b => b.IsActive);

        if (activeBranch is null)
        {
            return Array.Empty<InventoryCatalogItem>();
        }

        var outOfStock = await _inventoryCatalogQuery.SearchPageAsync(
            organizationId, activeBranch.Id, null, InventoryCatalogStatusFilter.OutOfStock, 0, LowStockTake, cancellationToken);
        var lowStock = await _inventoryCatalogQuery.SearchPageAsync(
            organizationId, activeBranch.Id, null, InventoryCatalogStatusFilter.LowStock, 0, LowStockTake, cancellationToken);

        var items = new List<InventoryCatalogItem>(outOfStock.Items.Count + lowStock.Items.Count);
        items.AddRange(outOfStock.Items);
        items.AddRange(lowStock.Items);

        return items;
    }

    public async Task<IReadOnlyList<OperatorActivityReportItem>> GetOperatorActivityAsync(
        DateTimeOffset fromUtc, DateTimeOffset toUtcExclusive, CancellationToken cancellationToken = default)
    {
        if (!TryGetAuthorizedOrganization(out var organizationId) || fromUtc >= toUtcExclusive)
        {
            return Array.Empty<OperatorActivityReportItem>();
        }

        return await _reportsQuery.GetOperatorActivityAsync(organizationId, fromUtc, toUtcExclusive, cancellationToken);
    }

    private bool TryGetAuthorizedOrganization(out OrganizationId organizationId)
    {
        var user = _currentUserSession.CurrentUser;

        if (user is null || !user.HasPermission(Permission.ViewReports))
        {
            organizationId = default;
            return false;
        }

        organizationId = user.OrganizationId;
        return true;
    }
}
