using Pos.Application.Sales.History;
using Pos.Domain.Common.Identifiers;

namespace Pos.Desktop.Tests.Sales.History;

internal sealed class FakeSalesHistoryService : ISalesHistoryService
{
    private readonly Func<SalesHistoryFilter, int, int, CancellationToken, Task<SalesHistoryPageResult>>? _searchPageHandler;
    private readonly Func<SalesHistoryFilter, CancellationToken, Task<SalesHistorySummary>>? _summaryHandler;
    private readonly Func<SaleId, CancellationToken, Task<SaleHistoryDetail?>>? _detailHandler;
    private readonly Func<CancellationToken, Task<SalesHistoryFilterOptions>>? _filterOptionsHandler;

    public FakeSalesHistoryService(
        Func<SalesHistoryFilter, int, int, CancellationToken, Task<SalesHistoryPageResult>>? searchPageHandler = null,
        Func<SalesHistoryFilter, CancellationToken, Task<SalesHistorySummary>>? summaryHandler = null,
        Func<SaleId, CancellationToken, Task<SaleHistoryDetail?>>? detailHandler = null,
        Func<CancellationToken, Task<SalesHistoryFilterOptions>>? filterOptionsHandler = null)
    {
        _searchPageHandler = searchPageHandler;
        _summaryHandler = summaryHandler;
        _detailHandler = detailHandler;
        _filterOptionsHandler = filterOptionsHandler;
    }

    public int SearchPageCallCount { get; private set; }

    public SalesHistoryFilter? LastFilter { get; private set; }

    public int? LastSkip { get; private set; }

    public int? LastTake { get; private set; }

    public int GetSummaryCallCount { get; private set; }

    public SalesHistoryFilter? LastSummaryFilter { get; private set; }

    public int GetDetailCallCount { get; private set; }

    public SaleId? LastDetailSaleId { get; private set; }

    public int GetFilterOptionsCallCount { get; private set; }

    public Task<SalesHistoryPageResult> SearchPageAsync(
        SalesHistoryFilter filter, int skip, int take, CancellationToken cancellationToken = default)
    {
        SearchPageCallCount++;
        LastFilter = filter;
        LastSkip = skip;
        LastTake = take;

        return _searchPageHandler is null
            ? Task.FromResult(SalesHistoryPageResult.Empty)
            : _searchPageHandler(filter, skip, take, cancellationToken);
    }

    public Task<SalesHistorySummary> GetSummaryAsync(
        SalesHistoryFilter filter, CancellationToken cancellationToken = default)
    {
        GetSummaryCallCount++;
        LastSummaryFilter = filter;

        return _summaryHandler is null
            ? Task.FromResult(SalesHistorySummary.Empty)
            : _summaryHandler(filter, cancellationToken);
    }

    public Task<SaleHistoryDetail?> GetDetailAsync(SaleId saleId, CancellationToken cancellationToken = default)
    {
        GetDetailCallCount++;
        LastDetailSaleId = saleId;

        return _detailHandler is null
            ? Task.FromResult<SaleHistoryDetail?>(null)
            : _detailHandler(saleId, cancellationToken);
    }

    public Task<SalesHistoryFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken = default)
    {
        GetFilterOptionsCallCount++;

        return _filterOptionsHandler is null
            ? Task.FromResult(SalesHistoryFilterOptions.Empty)
            : _filterOptionsHandler(cancellationToken);
    }
}
