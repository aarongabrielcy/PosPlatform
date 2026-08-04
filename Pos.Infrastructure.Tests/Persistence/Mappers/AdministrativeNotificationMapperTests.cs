using Pos.Domain.AdministrativeNotifications;
using Pos.Domain.Common.Identifiers;
using Pos.Infrastructure.Persistence.Mappers;

namespace Pos.Infrastructure.Tests.Persistence.Mappers;

public class AdministrativeNotificationMapperTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 1, 1, 9, 30, 0, TimeSpan.Zero);

    private static AdministrativeNotification CreateNotification(params UserId[] recipientUserIds) =>
        AdministrativeNotification.Create(
            AdministrativeNotificationId.New(), OrganizationId.New(), ProductAuditEventId.New(), CreatedAtUtc,
            recipientUserIds.Length == 0 ? [UserId.New()] : recipientUserIds);

    [Fact]
    public void ToRecordPreservesNotificationLevelFields()
    {
        var notification = CreateNotification();

        var record = AdministrativeNotificationMapper.ToRecord(notification);

        Assert.Equal(notification.Id.Value, record.Id);
        Assert.Equal(notification.OrganizationId.Value, record.OrganizationId);
        Assert.Equal(notification.ProductAuditEventId.Value, record.ProductAuditEventId);
        Assert.Equal(notification.CreatedAtUtc, record.CreatedAtUtc);
    }

    [Fact]
    public void ToRecordFlattensRecipientsWithTheNotificationIdAsForeignKey()
    {
        var userA = UserId.New();
        var userB = UserId.New();
        var notification = CreateNotification(userA, userB);

        var record = AdministrativeNotificationMapper.ToRecord(notification);

        Assert.Equal(2, record.Recipients.Count);
        Assert.All(record.Recipients, recipient => Assert.Equal(record.Id, recipient.NotificationId));
        Assert.Contains(record.Recipients, r => r.UserId == userA.Value);
        Assert.Contains(record.Recipients, r => r.UserId == userB.Value);
    }

    [Fact]
    public void ToRecordMapsUnreadRecipientsWithANullReadAtUtc()
    {
        var notification = CreateNotification();

        var record = AdministrativeNotificationMapper.ToRecord(notification);

        var recipient = Assert.Single(record.Recipients);
        Assert.Null(recipient.ReadAtUtc);
    }

    [Fact]
    public void ToRecordRejectsNullNotification()
    {
        Assert.Throws<ArgumentNullException>(() => AdministrativeNotificationMapper.ToRecord(null!));
    }
}
