using Pos.Domain.CashMovements;

namespace Pos.Application.CashMovements;

// Proyección inmutable de un CashMovement ya persistido, con el nombre del operador resuelto
// (igual criterio que RegisterSessionService.ResolveDisplayNameAsync): UserId es la fuente de
// verdad, ActorDisplayName es solo una comodidad de presentación que nunca invalida el histórico
// si el usuario se renombra o desactiva más adelante.
public sealed class CashMovementEntry
{
    public Guid Id { get; }

    public CashMovementType Type { get; }

    public decimal Amount { get; }

    public string Currency { get; }

    public string Reason { get; }

    public Guid ActorUserId { get; }

    public string ActorDisplayName { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public CashMovementEntry(
        Guid id,
        CashMovementType type,
        decimal amount,
        string currency,
        string reason,
        Guid actorUserId,
        string actorDisplayName,
        DateTimeOffset createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorDisplayName);

        Id = id;
        Type = type;
        Amount = amount;
        Currency = currency;
        Reason = reason;
        ActorUserId = actorUserId;
        ActorDisplayName = actorDisplayName;
        CreatedAtUtc = createdAtUtc;
    }
}
