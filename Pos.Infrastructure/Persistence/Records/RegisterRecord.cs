namespace Pos.Infrastructure.Persistence.Records;

internal sealed class RegisterRecord
{
    public Guid Id { get; set; }

    public Guid BranchId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
