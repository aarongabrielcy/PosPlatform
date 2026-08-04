namespace Pos.Infrastructure.Persistence.Records;

internal sealed class AdministrativeNotificationRecipientRecord
{
    public Guid NotificationId { get; set; }

    public Guid UserId { get; set; }

    public DateTimeOffset? ReadAtUtc { get; set; }

    public AdministrativeNotificationRecord Notification { get; set; } = null!;
}
