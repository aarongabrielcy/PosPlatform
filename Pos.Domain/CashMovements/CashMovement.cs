using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Common.ValueObjects;

namespace Pos.Domain.CashMovements;

// Hecho de negocio inmutable (BASIC-CASH-01, sección 5): una vez creado, un CashMovement nunca se
// edita, elimina ni revierte. Una corrección operativa se registra como un movimiento nuevo del
// tipo opuesto, con su propio Reason explicando la corrección — igual criterio que
// InventoryMovement (Domain/Inventory), que tampoco admite mutación posterior.
public sealed class CashMovement
{
    private const int MaxReasonLength = 200;

    public CashMovementId Id { get; }

    public RegisterSessionId RegisterSessionId { get; }

    public UserId ActorUserId { get; }

    public CashMovementType Type { get; }

    public Money Amount { get; }

    public string Reason { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    private CashMovement(
        CashMovementId id,
        RegisterSessionId registerSessionId,
        UserId actorUserId,
        CashMovementType type,
        Money amount,
        string reason,
        DateTimeOffset createdAtUtc)
    {
        Id = EnsureNotEmpty(id);
        RegisterSessionId = EnsureNotEmpty(registerSessionId);
        ActorUserId = EnsureNotEmpty(actorUserId);
        Type = EnsureDefined(type);
        Amount = EnsurePositive(amount);
        Reason = EnsureValidReason(reason);
        CreatedAtUtc = EnsureUtc(createdAtUtc, nameof(createdAtUtc));
    }

    public static CashMovement CreateCashIn(
        CashMovementId id,
        RegisterSessionId registerSessionId,
        UserId actorUserId,
        Money amount,
        string reason,
        DateTimeOffset createdAtUtc) =>
        new(id, registerSessionId, actorUserId, CashMovementType.CashIn, amount, reason, createdAtUtc);

    public static CashMovement CreateCashOut(
        CashMovementId id,
        RegisterSessionId registerSessionId,
        UserId actorUserId,
        Money amount,
        string reason,
        DateTimeOffset createdAtUtc) =>
        new(id, registerSessionId, actorUserId, CashMovementType.CashOut, amount, reason, createdAtUtc);

    private static CashMovementId EnsureNotEmpty(CashMovementId id)
    {
        if (id.Value == Guid.Empty)
        {
            throw new DomainValidationException("Id no puede ser vacío.");
        }

        return id;
    }

    private static RegisterSessionId EnsureNotEmpty(RegisterSessionId registerSessionId)
    {
        if (registerSessionId.Value == Guid.Empty)
        {
            throw new DomainValidationException("RegisterSessionId no puede ser vacío.");
        }

        return registerSessionId;
    }

    private static UserId EnsureNotEmpty(UserId actorUserId)
    {
        if (actorUserId.Value == Guid.Empty)
        {
            throw new DomainValidationException("ActorUserId no puede ser vacío.");
        }

        return actorUserId;
    }

    private static CashMovementType EnsureDefined(CashMovementType type)
    {
        if (!Enum.IsDefined(type))
        {
            throw new DomainValidationException("Type no es un valor válido de CashMovementType.");
        }

        return type;
    }

    private static Money EnsurePositive(Money amount)
    {
        if (amount is null)
        {
            throw new DomainValidationException("Amount es obligatorio.");
        }

        if (amount.Amount <= 0m)
        {
            throw new DomainValidationException("Amount debe ser mayor que cero.");
        }

        return amount;
    }

    private static string EnsureValidReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainValidationException("Reason es obligatorio.");
        }

        var trimmed = reason.Trim();

        if (trimmed.Length > MaxReasonLength)
        {
            throw new DomainValidationException($"Reason no puede superar {MaxReasonLength} caracteres.");
        }

        return trimmed;
    }

    private static DateTimeOffset EnsureUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new DomainValidationException($"{parameterName} debe tener Offset igual a TimeSpan.Zero.");
        }

        return value;
    }
}
