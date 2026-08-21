using Pos.Domain.CashMovements;

namespace Pos.Infrastructure.Persistence.Records;

internal sealed class CashMovementRecord
{
    public Guid Id { get; set; }

    public Guid RegisterSessionId { get; set; }

    public Guid ActorUserId { get; set; }

    public CashMovementType Type { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}
