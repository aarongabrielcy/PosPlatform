using Pos.Application.Sales.History;
using Pos.Domain.Common.Identifiers;

namespace Pos.Application.Tests.Sales.History;

internal sealed class FakeSalesHistoryQuery : ISalesHistoryQuery
{
    private readonly SalesHistoryPageResult _pageResult;
    private readonly SalesHistorySummary _summary;
    private readonly SaleHistoryDetail? _detail;
    private readonly SalesHistoryFilterOptions _filterOptions;

    public FakeSalesHistoryQuery(
        SalesHistoryPageResult? pageResult = null,
        SalesHistorySummary? summary = null,
        SaleHistoryDetail? detail = null,
        SalesHistoryFilterOptions? filterOptions = null)
    {
        _pageResult = pageResult ?? SalesHistoryPageResult.Empty;
        _summary = summary ?? SalesHistorySummary.Empty;
        _detail = detail;
        _filterOptions = filterOptions ?? SalesHistoryFilterOptions.Empty;
    }

    public int SearchPageCallCount { get; private set; }

    public OrganizationId? LastOrganizationId { get; private set; }

    public SalesHistoryFilter? LastFilter { get; private set; }

    public int? LastSkip { get; private set; }

    public int? LastTake { get; private set; }

    public int GetSummaryCallCount { get; private set; }

    public OrganizationId? LastSummaryOrganizationId { get; private set; }

    public SalesHistoryFilter? LastSummaryFilter { get; private set; }

    public int GetDetailCallCount { get; private set; }

    public OrganizationId? LastDetailOrganizationId { get; private set; }

    public SaleId? LastDetailSaleId { get; private set; }

    public int GetFilterOptionsCallCount { get; private set; }

    public OrganizationId? LastFilterOptionsOrganizationId { get; private set; }

    public Task<SalesHistoryPageResult> SearchPageAsync(
        OrganizationId organizationId, SalesHistoryFilter filter, int skip, int take, CancellationToken cancellationToken)
    {
        SearchPageCallCount++;
        LastOrganizationId = organizationId;
        LastFilter = filter;
        LastSkip = skip;
        LastTake = take;

        return Task.FromResult(_pageResult);
    }

    public Task<SalesHistorySummary> GetSummaryAsync(
        OrganizationId organizationId, SalesHistoryFilter filter, CancellationToken cancellationToken)
    {
        GetSummaryCallCount++;
        LastSummaryOrganizationId = organizationId;
        LastSummaryFilter = filter;

        return Task.FromResult(_summary);
    }

    public Task<SaleHistoryDetail?> GetDetailAsync(
        OrganizationId organizationId, SaleId saleId, CancellationToken cancellationToken)
    {
        GetDetailCallCount++;
        LastDetailOrganizationId = organizationId;
        LastDetailSaleId = saleId;

        return Task.FromResult(_detail);
    }

    public Task<SalesHistoryFilterOptions> GetFilterOptionsAsync(
        OrganizationId organizationId, CancellationToken cancellationToken)
    {
        GetFilterOptionsCallCount++;
        LastFilterOptionsOrganizationId = organizationId;

        return Task.FromResult(_filterOptions);
    }
}
