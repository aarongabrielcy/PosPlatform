using Pos.Application.Authentication;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;

namespace Pos.Application.Sales.History;

public sealed class SalesHistoryService : ISalesHistoryService
{
    private readonly ICurrentUserSession _currentUserSession;
    private readonly ISalesHistoryQuery _salesHistoryQuery;

    public SalesHistoryService(ICurrentUserSession currentUserSession, ISalesHistoryQuery salesHistoryQuery)
    {
        _currentUserSession = currentUserSession ?? throw new ArgumentNullException(nameof(currentUserSession));
        _salesHistoryQuery = salesHistoryQuery ?? throw new ArgumentNullException(nameof(salesHistoryQuery));
    }

    public async Task<SalesHistoryPageResult> SearchPageAsync(
        SalesHistoryFilter filter, int skip, int take, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var user = _currentUserSession.CurrentUser;

        if (user is null || !user.HasPermission(Permission.ViewReports))
        {
            return SalesHistoryPageResult.Empty;
        }

        return await _salesHistoryQuery.SearchPageAsync(user.OrganizationId, filter, skip, take, cancellationToken);
    }

    public async Task<SalesHistorySummary> GetSummaryAsync(
        SalesHistoryFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var user = _currentUserSession.CurrentUser;

        if (user is null || !user.HasPermission(Permission.ViewReports))
        {
            return SalesHistorySummary.Empty;
        }

        return await _salesHistoryQuery.GetSummaryAsync(user.OrganizationId, filter, cancellationToken);
    }

    public async Task<SaleHistoryDetail?> GetDetailAsync(SaleId saleId, CancellationToken cancellationToken = default)
    {
        var user = _currentUserSession.CurrentUser;

        if (user is null || !user.HasPermission(Permission.ViewReports))
        {
            return null;
        }

        return await _salesHistoryQuery.GetDetailAsync(user.OrganizationId, saleId, cancellationToken);
    }

    public async Task<SalesHistoryFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken = default)
    {
        var user = _currentUserSession.CurrentUser;

        if (user is null || !user.HasPermission(Permission.ViewReports))
        {
            return SalesHistoryFilterOptions.Empty;
        }

        return await _salesHistoryQuery.GetFilterOptionsAsync(user.OrganizationId, cancellationToken);
    }
}
