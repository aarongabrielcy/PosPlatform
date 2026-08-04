using Pos.Domain.AdministrativeNotifications;
using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;

namespace Pos.Domain.Tests.AdministrativeNotifications;

public class AdministrativeNotificationRecipientTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ReadAtUtc = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    private static AdministrativeNotificationRecipient CreateRecipient(UserId? userId = null)
    {
        var notification = AdministrativeNotification.Create(
            AdministrativeNotificationId.New(), OrganizationId.New(), ProductAuditEventId.New(), CreatedAtUtc,
            [userId ?? UserId.New()]);

        return Assert.Single(notification.Recipients);
    }

    [Fact]
    public void MarkReadSetsReadAtUtc()
    {
        var recipient = CreateRecipient();

        recipient.MarkRead(ReadAtUtc);

        Assert.True(recipient.IsRead);
        Assert.Equal(ReadAtUtc, recipient.ReadAtUtc);
    }

    [Fact]
    public void MarkReadIsIdempotentAndKeepsTheFirstTimestamp()
    {
        var recipient = CreateRecipient();
        var laterUtc = ReadAtUtc.AddHours(1);

        recipient.MarkRead(ReadAtUtc);
        recipient.MarkRead(laterUtc);

        Assert.Equal(ReadAtUtc, recipient.ReadAtUtc);
    }

    [Fact]
    public void MarkReadRejectsNonUtcTimestamp()
    {
        var recipient = CreateRecipient();
        var nonUtc = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.FromHours(-5));

        Assert.Throws<DomainValidationException>(() => recipient.MarkRead(nonUtc));
    }
}
