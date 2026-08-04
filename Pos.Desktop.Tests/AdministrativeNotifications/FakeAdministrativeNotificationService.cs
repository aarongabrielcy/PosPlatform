using Pos.Application.AdministrativeNotifications;
using Pos.Domain.Common.Identifiers;

namespace Pos.Desktop.Tests.AdministrativeNotifications;

internal sealed class FakeAdministrativeNotificationService : IAdministrativeNotificationService
{
    public AdministrativeNotificationPageResult PageResult { get; set; } = AdministrativeNotificationPageResult.Empty;

    public int UnreadCount { get; set; }

    public bool MarkReadResult { get; set; } = true;

    public Exception? ThrowOnGetNotifications { get; set; }

    public int GetNotificationsCallCount { get; private set; }

    public int GetUnreadCountCallCount { get; private set; }

    public int MarkReadCallCount { get; private set; }

    public AdministrativeNotificationId? LastMarkReadNotificationId { get; private set; }

    public Task<AdministrativeNotificationPageResult> GetNotificationsAsync(
        int skip, int take, CancellationToken cancellationToken = default)
    {
        GetNotificationsCallCount++;

        if (ThrowOnGetNotifications is not null)
        {
            throw ThrowOnGetNotifications;
        }

        return Task.FromResult(PageResult);
    }

    public Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        GetUnreadCountCallCount++;

        return Task.FromResult(UnreadCount);
    }

    public Task<bool> MarkReadAsync(AdministrativeNotificationId notificationId, CancellationToken cancellationToken = default)
    {
        MarkReadCallCount++;
        LastMarkReadNotificationId = notificationId;

        return Task.FromResult(MarkReadResult);
    }
}
