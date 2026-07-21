namespace Pos.Infrastructure.Persistence.Records;

internal sealed class OrganizationRecord
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
