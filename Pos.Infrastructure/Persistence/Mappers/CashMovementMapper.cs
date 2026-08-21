using Pos.Domain.CashMovements;
using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Common.ValueObjects;
using Pos.Infrastructure.Persistence.Exceptions;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Persistence.Mappers;

internal static class CashMovementMapper
{
    internal static CashMovementRecord ToRecord(CashMovement movement)
    {
        ArgumentNullException.ThrowIfNull(movement);

        return new CashMovementRecord
        {
            Id = movement.Id.Value,
            RegisterSessionId = movement.RegisterSessionId.Value,
            ActorUserId = movement.ActorUserId.Value,
            Type = movement.Type,
            Amount = movement.Amount.Amount,
            Currency = movement.Amount.Currency,
            Reason = movement.Reason,
            CreatedAtUtc = movement.CreatedAtUtc,
        };
    }

    internal static CashMovement ToDomain(CashMovementRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        try
        {
            var id = new CashMovementId(record.Id);
            var registerSessionId = new RegisterSessionId(record.RegisterSessionId);
            var actorUserId = new UserId(record.ActorUserId);
            var amount = new Money(record.Amount, record.Currency);

            return record.Type switch
            {
                CashMovementType.CashIn => CashMovement.CreateCashIn(
                    id, registerSessionId, actorUserId, amount, record.Reason, record.CreatedAtUtc),
                CashMovementType.CashOut => CashMovement.CreateCashOut(
                    id, registerSessionId, actorUserId, amount, record.Reason, record.CreatedAtUtc),
                _ => throw new PersistenceDataException(
                    $"CashMovementRecord con Id '{record.Id}' tiene un Type no soportado: '{record.Type}'."),
            };
        }
        catch (DomainValidationException ex)
        {
            throw new PersistenceDataException(
                $"CashMovementRecord con Id '{record.Id}' contiene datos inválidos: {ex.Message}", ex);
        }
    }
}
