using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Common.ValueObjects;

namespace Pos.Domain.RegisterSessions;

public sealed class RegisterSession
{
    public RegisterSessionId Id { get; }

    public RegisterId RegisterId { get; }

    public UserId OpenedByUserId { get; }

    public UserId? ClosedByUserId { get; private set; }

    public Money OpeningFloat { get; }

    public Money? ExpectedCash { get; private set; }

    public Money? CountedCash { get; private set; }

    public Money? CashDifference { get; private set; }

    public RegisterSessionStatus Status { get; private set; }

    public DateTimeOffset OpenedAtUtc { get; }

    public DateTimeOffset? ClosedAtUtc { get; private set; }

    public RegisterSession(
        RegisterSessionId id,
        RegisterId registerId,
        UserId openedByUserId,
        Money openingFloat,
        DateTimeOffset openedAtUtc)
    {
        Id = EnsureNotEmpty(id);
        RegisterId = EnsureNotEmpty(registerId);
        OpenedByUserId = EnsureNotEmpty(openedByUserId);
        OpeningFloat = EnsureNonNegative(openingFloat, nameof(openingFloat));
        OpenedAtUtc = EnsureUtc(openedAtUtc, nameof(openedAtUtc));
        Status = RegisterSessionStatus.Open;
    }

    public void Close(
        UserId closedByUserId,
        Money expectedCash,
        Money countedCash,
        DateTimeOffset closedAtUtc)
    {
        if (Status != RegisterSessionStatus.Open)
        {
            throw new DomainValidationException("Solo puede cerrarse una sesión con Status Open.");
        }

        var validClosedByUserId = EnsureNotEmpty(closedByUserId);
        var validExpectedCash = EnsureNonNegative(expectedCash, nameof(expectedCash));
        var validCountedCash = EnsureNonNegative(countedCash, nameof(countedCash));
        var validClosedAtUtc = EnsureUtc(closedAtUtc, nameof(closedAtUtc));

        EnsureSameCurrency(validExpectedCash, nameof(expectedCash));
        EnsureSameCurrency(validCountedCash, nameof(countedCash));

        if (validClosedAtUtc < OpenedAtUtc)
        {
            throw new DomainValidationException("ClosedAtUtc no puede ser anterior a OpenedAtUtc.");
        }

        var cashDifference = validCountedCash - validExpectedCash;

        ClosedByUserId = validClosedByUserId;
        ExpectedCash = validExpectedCash;
        CountedCash = validCountedCash;
        CashDifference = cashDifference;
        ClosedAtUtc = validClosedAtUtc;
        Status = RegisterSessionStatus.Closed;
    }

    private static RegisterSessionId EnsureNotEmpty(RegisterSessionId id)
    {
        if (id.Value == Guid.Empty)
        {
            throw new DomainValidationException("Id no puede ser vacío.");
        }

        return id;
    }

    private static RegisterId EnsureNotEmpty(RegisterId registerId)
    {
        if (registerId.Value == Guid.Empty)
        {
            throw new DomainValidationException("RegisterId no puede ser vacío.");
        }

        return registerId;
    }

    private static UserId EnsureNotEmpty(UserId userId)
    {
        if (userId.Value == Guid.Empty)
        {
            throw new DomainValidationException("UserId no puede ser vacío.");
        }

        return userId;
    }

    private static Money EnsureNonNegative(Money money, string parameterName)
    {
        if (money is null)
        {
            throw new DomainValidationException($"{parameterName} es obligatorio.");
        }

        if (money.Amount < 0m)
        {
            throw new DomainValidationException($"{parameterName} no puede ser negativo.");
        }

        return money;
    }

    private void EnsureSameCurrency(Money money, string parameterName)
    {
        if (money.Currency != OpeningFloat.Currency)
        {
            throw new DomainValidationException(
                $"{parameterName}.Currency debe coincidir con OpeningFloat.Currency.");
        }
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
