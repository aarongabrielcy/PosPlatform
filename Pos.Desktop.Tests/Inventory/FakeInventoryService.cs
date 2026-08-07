using Pos.Application.Inventory;

namespace Pos.Desktop.Tests.Inventory;

internal sealed class FakeInventoryService : IInventoryService
{
    private readonly Func<string?, InventoryCatalogStatusFilter, int, int, CancellationToken, Task<InventoryCatalogPageResult>>? _catalogHandler;
    private readonly Func<CancellationToken, Task<InventorySummary>>? _summaryHandler;
    private readonly Func<InventoryMovementFilter, int, int, CancellationToken, Task<InventoryMovementPageResult>>? _movementHandler;

    public FakeInventoryService(
        Func<string?, InventoryCatalogStatusFilter, int, int, CancellationToken, Task<InventoryCatalogPageResult>>? catalogHandler = null,
        Func<CancellationToken, Task<InventorySummary>>? summaryHandler = null,
        Func<InventoryMovementFilter, int, int, CancellationToken, Task<InventoryMovementPageResult>>? movementHandler = null)
    {
        _catalogHandler = catalogHandler;
        _summaryHandler = summaryHandler;
        _movementHandler = movementHandler;
    }

    public int GetCatalogPageCallCount { get; private set; }

    public string? LastSearchTerm { get; private set; }

    public InventoryCatalogStatusFilter? LastFilter { get; private set; }

    public int? LastSkip { get; private set; }

    public int? LastTake { get; private set; }

    public int GetSummaryCallCount { get; private set; }

    public int GetMovementPageCallCount { get; private set; }

    public InventoryMovementFilter? LastMovementFilter { get; private set; }

    public Task<InventoryCatalogPageResult> GetCatalogPageAsync(
        string? searchTerm,
        InventoryCatalogStatusFilter filter,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        GetCatalogPageCallCount++;
        LastSearchTerm = searchTerm;
        LastFilter = filter;
        LastSkip = skip;
        LastTake = take;

        return _catalogHandler is null
            ? Task.FromResult(InventoryCatalogPageResult.Empty)
            : _catalogHandler(searchTerm, filter, skip, take, cancellationToken);
    }

    public Task<InventorySummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        GetSummaryCallCount++;

        return _summaryHandler is null
            ? Task.FromResult(InventorySummary.Empty)
            : _summaryHandler(cancellationToken);
    }

    public Task<InventoryMovementPageResult> GetMovementPageAsync(
        InventoryMovementFilter filter, int skip, int take, CancellationToken cancellationToken = default)
    {
        GetMovementPageCallCount++;
        LastMovementFilter = filter;

        return _movementHandler is null
            ? Task.FromResult(InventoryMovementPageResult.Empty)
            : _movementHandler(filter, skip, take, cancellationToken);
    }
}
