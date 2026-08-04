using Pos.Application.AdministrativeNotifications;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;

namespace Pos.Application.Tests.AdministrativeNotifications;

internal sealed class FakeAdministrativeNotificationAudienceQuery : IAdministrativeNotificationAudienceQuery
{
    private readonly IReadOnlyList<UserId> _eligibleUserIds;

    public FakeAdministrativeNotificationAudienceQuery(IReadOnlyList<UserId>? eligibleUserIds = null)
    {
        _eligibleUserIds = eligibleUserIds ?? [];
    }

    public int CallCount { get; private set; }

    public OrganizationId? LastOrganizationId { get; private set; }

    public Permission? LastPermission { get; private set; }

    public Task<IReadOnlyList<UserId>> GetEligibleUserIdsAsync(
        OrganizationId organizationId, Permission permission, CancellationToken cancellationToken)
    {
        CallCount++;
        LastOrganizationId = organizationId;
        LastPermission = permission;

        return Task.FromResult(_eligibleUserIds);
    }
}
