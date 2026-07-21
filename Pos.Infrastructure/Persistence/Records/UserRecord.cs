namespace Pos.Infrastructure.Persistence.Records;

internal sealed class UserRecord
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid RoleId { get; set; }

    public string Username { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
