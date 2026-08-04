using Pos.Application.AdministrativeNotifications;
using Pos.Domain.AdministrativeNotifications;
using Pos.Domain.Common.Identifiers;

namespace Pos.Application.Tests.AdministrativeNotifications;

internal sealed class FakeAdministrativeNotificationRepository : IAdministrativeNotificationRepository
{
    private readonly List<AdministrativeNotification> _added = [];

    public int AddCallCount { get; private set; }

    public IReadOnlyList<AdministrativeNotification> Added => _added;

    public int MarkReadCallCount { get; private set; }

    public UserId? LastMarkReadUserId { get; private set; }

    public AdministrativeNotificationId? LastMarkReadNotificationId { get; private set; }

    public Task AddAsync(AdministrativeNotification notification, CancellationToken cancellationToken)
    {
        AddCallCount++;
        _added.Add(notification);

        return Task.CompletedTask;
    }

    public Task MarkReadAsync(
        AdministrativeNotificationId notificationId, UserId userId, DateTimeOffset readAtUtc, CancellationToken cancellationToken)
    {
        MarkReadCallCount++;
        LastMarkReadNotificationId = notificationId;
        LastMarkReadUserId = userId;

        var notification = _added.FirstOrDefault(n => n.Id == notificationId);
        var recipient = notification?.Recipients.FirstOrDefault(r => r.UserId == userId);
        recipient?.MarkRead(readAtUtc);

        return Task.CompletedTask;
    }
}
