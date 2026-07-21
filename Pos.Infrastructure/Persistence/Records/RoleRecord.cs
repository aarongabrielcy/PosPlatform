namespace Pos.Infrastructure.Persistence.Records;

internal sealed class RoleRecord
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public List<RolePermissionRecord> Permissions { get; set; } = new();
}
