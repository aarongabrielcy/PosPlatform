namespace Pos.Infrastructure.Persistence.Records;

internal sealed class RolePermissionRecord
{
    public Guid RoleId { get; set; }

    public string Permission { get; set; } = string.Empty;

    public RoleRecord Role { get; set; } = null!;
}
