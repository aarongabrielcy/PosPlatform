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
        : this(
            id,
            registerId,
            openedByUserId,
            openingFloat,
            openedAtUtc,
            RegisterSessionStatus.Open,
            closedByUserId: null,
            expectedCash: null,
            countedCash: null,
            cashDifference: null,
            closedAtUtc: null)
    {
    }

    private RegisterSession(
        RegisterSessionId id,
        RegisterId registerId,
        UserId openedByUserId,
        Money openingFloat,
        DateTimeOffset openedAtUtc,
        RegisterSessionStatus status,
        UserId? closedByUserId,
        Money? expectedCash,
        Money? countedCash,
        Money? cashDifference,
        DateTimeOffset? closedAtUtc)
    {
        Id = EnsureNotEmpty(id);
        RegisterId = EnsureNotEmpty(registerId);
        OpenedByUserId = EnsureNotEmpty(openedByUserId);
        OpeningFloat = EnsureNonNegative(openingFloat, nameof(openingFloat));
        OpenedAtUtc = EnsureUtc(openedAtUtc, nameof(openedAtUtc));

        switch (status)
        {
            case RegisterSessionStatus.Open:
                EnsureNoCloseDataForOpenStatus(closedByUserId, expectedCash, countedCash, cashDifference, closedAtUtc);
                Status = RegisterSessionStatus.Open;
                break;

            case RegisterSessionStatus.Closed:
                AssignClosedState(closedByUserId, expectedCash, countedCash, cashDifference, closedAtUtc);
                Status = RegisterSessionStatus.Closed;
                break;

            default:
                throw new DomainValidationException($"Status '{status}' no es un RegisterSessionStatus válido.");
        }
    }

    // Reconstruye estado histórico ya persistido, sin recalcular mediante Close(). Valida coherencia
    // entre Status y los datos de cierre, incluyendo que CashDifference == CountedCash - ExpectedCash.
    public static RegisterSession Rehydrate(
        RegisterSessionId id,
        RegisterId registerId,
        UserId openedByUserId,
        Money openingFloat,
        DateTimeOffset openedAtUtc,
        RegisterSessionStatus status,
        UserId? closedByUserId,
        Money? expectedCash,
        Money? countedCash,
        Money? cashDifference,
        DateTimeOffset? closedAtUtc) =>
        new(
            id,
            registerId,
            openedByUserId,
            openingFloat,
            openedAtUtc,
            status,
            closedByUserId,
            expectedCash,
            countedCash,
            cashDifference,
            closedAtUtc);

    private static void EnsureNoCloseDataForOpenStatus(
        UserId? closedByUserId,
        Money? expectedCash,
        Money? countedCash,
        Money? cashDifference,
        DateTimeOffset? closedAtUtc)
    {
        if (closedByUserId is not null
            || expectedCash is not null
            || countedCash is not null
            || cashDifference is not null
            || closedAtUtc is not null)
        {
            throw new DomainValidationException("Una sesión con Status Open no puede tener datos de cierre.");
        }
    }

    private void AssignClosedState(
        UserId? closedByUserId,
        Money? expectedCash,
        Money? countedCash,
        Money? cashDifference,
        DateTimeOffset? closedAtUtc)
    {
        if (closedByUserId is null)
        {
            throw new DomainValidationException("ClosedByUserId es obligatorio para una sesión Closed.");
        }

        if (expectedCash is null)
        {
            throw new DomainValidationException("ExpectedCash es obligatorio para una sesión Closed.");
        }

        if (countedCash is null)
        {
            throw new DomainValidationException("CountedCash es obligatorio para una sesión Closed.");
        }

        if (cashDifference is null)
        {
            throw new DomainValidationException("CashDifference es obligatorio para una sesión Closed.");
        }

        if (closedAtUtc is null)
        {
            throw new DomainValidationException("ClosedAtUtc es obligatorio para una sesión Closed.");
        }

        var validClosedByUserId = EnsureNotEmpty(closedByUserId.Value);
        var validExpectedCash = EnsureNonNegative(expectedCash, nameof(expectedCash));
        var validCountedCash = EnsureNonNegative(countedCash, nameof(countedCash));
        var validClosedAtUtc = EnsureUtc(closedAtUtc.Value, nameof(closedAtUtc));

        EnsureSameCurrency(validExpectedCash, nameof(expectedCash));
        EnsureSameCurrency(validCountedCash, nameof(countedCash));
        EnsureSameCurrency(cashDifference, nameof(cashDifference));

        if (validClosedAtUtc < OpenedAtUtc)
        {
            throw new DomainValidationException("ClosedAtUtc no puede ser anterior a OpenedAtUtc.");
        }

        var expectedDifference = validCountedCash - validExpectedCash;

        if (cashDifference != expectedDifference)
        {
            throw new DomainValidationException(
                "CashDifference no coincide con CountedCash - ExpectedCash.");
        }

        ClosedByUserId = validClosedByUserId;
        ExpectedCash = validExpectedCash;
        CountedCash = validCountedCash;
        CashDifference = cashDifference;
        ClosedAtUtc = validClosedAtUtc;
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
