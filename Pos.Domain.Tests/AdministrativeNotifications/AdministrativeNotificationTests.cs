using Pos.Domain.AdministrativeNotifications;
using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;

namespace Pos.Domain.Tests.AdministrativeNotifications;

public class AdministrativeNotificationTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateKeepsIdentifiers()
    {
        var id = AdministrativeNotificationId.New();
        var organizationId = OrganizationId.New();
        var auditEventId = ProductAuditEventId.New();
        var userId = UserId.New();

        var notification = AdministrativeNotification.Create(id, organizationId, auditEventId, CreatedAtUtc, [userId]);

        Assert.Equal(id, notification.Id);
        Assert.Equal(organizationId, notification.OrganizationId);
        Assert.Equal(auditEventId, notification.ProductAuditEventId);
        Assert.Equal(CreatedAtUtc, notification.CreatedAtUtc);
    }

    [Fact]
    public void CreateBuildsOneRecipientPerUserId()
    {
        var userIdA = UserId.New();
        var userIdB = UserId.New();

        var notification = AdministrativeNotification.Create(
            AdministrativeNotificationId.New(), OrganizationId.New(), ProductAuditEventId.New(), CreatedAtUtc,
            [userIdA, userIdB]);

        Assert.Equal(2, notification.Recipients.Count);
        Assert.Contains(notification.Recipients, r => r.UserId == userIdA);
        Assert.Contains(notification.Recipients, r => r.UserId == userIdB);
    }

    [Fact]
    public void CreateRecipientsStartUnread()
    {
        var notification = AdministrativeNotification.Create(
            AdministrativeNotificationId.New(), OrganizationId.New(), ProductAuditEventId.New(), CreatedAtUtc,
            [UserId.New()]);

        var recipient = Assert.Single(notification.Recipients);
        Assert.False(recipient.IsRead);
        Assert.Null(recipient.ReadAtUtc);
    }

    [Fact]
    public void CreateStampsEachRecipientWithTheNotificationId()
    {
        var notification = AdministrativeNotification.Create(
            AdministrativeNotificationId.New(), OrganizationId.New(), ProductAuditEventId.New(), CreatedAtUtc,
            [UserId.New()]);

        var recipient = Assert.Single(notification.Recipients);
        Assert.Equal(notification.Id, recipient.NotificationId);
    }

    [Fact]
    public void CreateRejectsEmptyRecipientList()
    {
        Assert.Throws<DomainValidationException>(() => AdministrativeNotification.Create(
            AdministrativeNotificationId.New(), OrganizationId.New(), ProductAuditEventId.New(), CreatedAtUtc, []));
    }

    [Fact]
    public void CreateRejectsDuplicateRecipientUserIds()
    {
        var userId = UserId.New();

        Assert.Throws<DomainValidationException>(() => AdministrativeNotification.Create(
            AdministrativeNotificationId.New(), OrganizationId.New(), ProductAuditEventId.New(), CreatedAtUtc,
            [userId, userId]));
    }

    [Fact]
    public void CreateRejectsDefaultOrganizationId()
    {
        Assert.Throws<DomainValidationException>(() => AdministrativeNotification.Create(
            AdministrativeNotificationId.New(), default, ProductAuditEventId.New(), CreatedAtUtc, [UserId.New()]));
    }

    [Fact]
    public void CreateRejectsDefaultProductAuditEventId()
    {
        Assert.Throws<DomainValidationException>(() => AdministrativeNotification.Create(
            AdministrativeNotificationId.New(), OrganizationId.New(), default, CreatedAtUtc, [UserId.New()]));
    }

    [Fact]
    public void CreateRejectsNonUtcCreatedAtUtc()
    {
        var nonUtc = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.FromHours(-5));

        Assert.Throws<DomainValidationException>(() => AdministrativeNotification.Create(
            AdministrativeNotificationId.New(), OrganizationId.New(), ProductAuditEventId.New(), nonUtc, [UserId.New()]));
    }
}
