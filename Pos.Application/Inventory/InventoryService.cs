using Pos.Application.Authentication;
using Pos.Application.RegisterSessions;
using Pos.Domain.Security;

namespace Pos.Application.Inventory;

public sealed class InventoryService : IInventoryService
{
    private readonly ICurrentUserSession _currentUserSession;
    private readonly ICurrentRegisterSession _currentRegisterSession;
    private readonly IInventoryCatalogQuery _inventoryCatalogQuery;
    private readonly IInventoryMovementQuery _inventoryMovementQuery;

    public InventoryService(
        ICurrentUserSession currentUserSession,
        ICurrentRegisterSession currentRegisterSession,
        IInventoryCatalogQuery inventoryCatalogQuery,
        IInventoryMovementQuery inventoryMovementQuery)
    {
        _currentUserSession = currentUserSession ?? throw new ArgumentNullException(nameof(currentUserSession));
        _currentRegisterSession = currentRegisterSession ?? throw new ArgumentNullException(nameof(currentRegisterSession));
        _inventoryCatalogQuery = inventoryCatalogQuery ?? throw new ArgumentNullException(nameof(inventoryCatalogQuery));
        _inventoryMovementQuery = inventoryMovementQuery ?? throw new ArgumentNullException(nameof(inventoryMovementQuery));
    }

    public async Task<InventoryCatalogPageResult> GetCatalogPageAsync(
        string? searchTerm,
        InventoryCatalogStatusFilter filter,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var user = _currentUserSession.CurrentUser;
        var registerSession = _currentRegisterSession.Current;

        if (user is null || !user.HasPermission(Permission.ViewInventory) || registerSession is null)
        {
            return InventoryCatalogPageResult.Empty;
        }

        return await _inventoryCatalogQuery.SearchPageAsync(
            user.OrganizationId, registerSession.BranchId, searchTerm, filter, skip, take, cancellationToken);
    }

    public async Task<InventorySummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var user = _currentUserSession.CurrentUser;
        var registerSession = _currentRegisterSession.Current;

        if (user is null || !user.HasPermission(Permission.ViewInventory) || registerSession is null)
        {
            return InventorySummary.Empty;
        }

        return await _inventoryCatalogQuery.GetSummaryAsync(user.OrganizationId, registerSession.BranchId, cancellationToken);
    }

    public async Task<InventoryMovementPageResult> GetMovementPageAsync(
        InventoryMovementFilter filter,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var user = _currentUserSession.CurrentUser;
        var registerSession = _currentRegisterSession.Current;

        if (user is null || !user.HasPermission(Permission.ViewInventory) || registerSession is null)
        {
            return InventoryMovementPageResult.Empty;
        }

        return await _inventoryMovementQuery.SearchPageAsync(
            user.OrganizationId, registerSession.BranchId, filter, skip, take, cancellationToken);
    }
}
