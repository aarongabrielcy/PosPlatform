using Pos.Application.Inventory;
using Pos.Domain.Common.Identifiers;

namespace Pos.Application.Tests.Inventory;

internal sealed class FakeInventoryMovementQuery : IInventoryMovementQuery
{
    private readonly InventoryMovementPageResult _result;

    public FakeInventoryMovementQuery(InventoryMovementPageResult? result = null)
    {
        _result = result ?? InventoryMovementPageResult.Empty;
    }

    public int SearchPageCallCount { get; private set; }

    public OrganizationId? LastOrganizationId { get; private set; }

    public BranchId? LastBranchId { get; private set; }

    public InventoryMovementFilter? LastFilter { get; private set; }

    public int? LastSkip { get; private set; }

    public int? LastTake { get; private set; }

    public Task<InventoryMovementPageResult> SearchPageAsync(
        OrganizationId organizationId,
        BranchId branchId,
        InventoryMovementFilter filter,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        SearchPageCallCount++;
        LastOrganizationId = organizationId;
        LastBranchId = branchId;
        LastFilter = filter;
        LastSkip = skip;
        LastTake = take;

        return Task.FromResult(_result);
    }
}
