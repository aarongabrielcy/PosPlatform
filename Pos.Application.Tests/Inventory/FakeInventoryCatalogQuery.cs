using Pos.Application.Inventory;
using Pos.Domain.Common.Identifiers;

namespace Pos.Application.Tests.Inventory;

internal sealed class FakeInventoryCatalogQuery : IInventoryCatalogQuery
{
    private readonly InventoryCatalogPageResult _result;
    private readonly InventorySummary _summary;

    public FakeInventoryCatalogQuery(InventoryCatalogPageResult? result = null, InventorySummary? summary = null)
    {
        _result = result ?? InventoryCatalogPageResult.Empty;
        _summary = summary ?? InventorySummary.Empty;
    }

    public int SearchPageCallCount { get; private set; }

    public int GetSummaryCallCount { get; private set; }

    public OrganizationId? LastOrganizationId { get; private set; }

    public BranchId? LastBranchId { get; private set; }

    public string? LastSearchTerm { get; private set; }

    public InventoryCatalogStatusFilter? LastFilter { get; private set; }

    public int? LastSkip { get; private set; }

    public int? LastTake { get; private set; }

    public Task<InventoryCatalogPageResult> SearchPageAsync(
        OrganizationId organizationId,
        BranchId branchId,
        string? searchTerm,
        InventoryCatalogStatusFilter filter,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        SearchPageCallCount++;
        LastOrganizationId = organizationId;
        LastBranchId = branchId;
        LastSearchTerm = searchTerm;
        LastFilter = filter;
        LastSkip = skip;
        LastTake = take;

        return Task.FromResult(_result);
    }

    public Task<InventorySummary> GetSummaryAsync(
        OrganizationId organizationId, BranchId branchId, CancellationToken cancellationToken)
    {
        GetSummaryCallCount++;
        LastOrganizationId = organizationId;
        LastBranchId = branchId;

        return Task.FromResult(_summary);
    }
}
