using Pos.Application.AdministrativeNotifications;
using Pos.Domain.Common.Identifiers;

namespace Pos.Application.Tests.AdministrativeNotifications;

internal sealed class FakeAdministrativeNotificationQuery : IAdministrativeNotificationQuery
{
    private readonly AdministrativeNotificationPageResult _pageResult;
    private readonly int _unreadCount;

    public FakeAdministrativeNotificationQuery(
        AdministrativeNotificationPageResult? pageResult = null, int unreadCount = 0)
    {
        _pageResult = pageResult ?? AdministrativeNotificationPageResult.Empty;
        _unreadCount = unreadCount;
    }

    public int GetForUserCallCount { get; private set; }

    public int GetUnreadCountCallCount { get; private set; }

    public OrganizationId? LastOrganizationId { get; private set; }

    public UserId? LastUserId { get; private set; }

    public Task<AdministrativeNotificationPageResult> GetForUserAsync(
        OrganizationId organizationId, UserId userId, int skip, int take, CancellationToken cancellationToken)
    {
        GetForUserCallCount++;
        LastOrganizationId = organizationId;
        LastUserId = userId;

        return Task.FromResult(_pageResult);
    }

    public Task<int> GetUnreadCountAsync(OrganizationId organizationId, UserId userId, CancellationToken cancellationToken)
    {
        GetUnreadCountCallCount++;
        LastOrganizationId = organizationId;
        LastUserId = userId;

        return Task.FromResult(_unreadCount);
    }
}
