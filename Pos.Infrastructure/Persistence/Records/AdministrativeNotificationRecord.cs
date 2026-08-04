namespace Pos.Infrastructure.Persistence.Records;

internal sealed class AdministrativeNotificationRecord
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid ProductAuditEventId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public List<AdministrativeNotificationRecipientRecord> Recipients { get; set; } = [];
}
