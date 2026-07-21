using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Common.ValueObjects;
using Pos.Domain.RegisterSessions;

namespace Pos.Domain.Tests.RegisterSessions;

public class RegisterSessionTests
{
    private static readonly DateTimeOffset OpenedAtUtc = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ClosedAtUtc = new(2026, 1, 1, 20, 0, 0, TimeSpan.Zero);

    private static RegisterSession CreateOpenSession(Money? openingFloat = null) =>
        new(
            RegisterSessionId.New(),
            RegisterId.New(),
            UserId.New(),
            openingFloat ?? new Money(100m, "USD"),
            OpenedAtUtc);

    // ---------- Creación ----------

    [Fact]
    public void NewSessionStartsOpen()
    {
        var session = CreateOpenSession();

        Assert.Equal(RegisterSessionStatus.Open, session.Status);
    }

    [Fact]
    public void NewSessionKeepsIdRegisterIdAndOpenedByUserId()
    {
        var id = RegisterSessionId.New();
        var registerId = RegisterId.New();
        var openedByUserId = UserId.New();

        var session = new RegisterSession(id, registerId, openedByUserId, new Money(100m, "USD"), OpenedAtUtc);

        Assert.Equal(id, session.Id);
        Assert.Equal(registerId, session.RegisterId);
        Assert.Equal(openedByUserId, session.OpenedByUserId);
    }

    [Fact]
    public void NewSessionKeepsOpeningFloat()
    {
        var openingFloat = new Money(250m, "USD");

        var session = CreateOpenSession(openingFloat);

        Assert.Equal(openingFloat, session.OpeningFloat);
    }

    [Fact]
    public void NewSessionKeepsOpenedAtUtc()
    {
        var session = CreateOpenSession();

        Assert.Equal(OpenedAtUtc, session.OpenedAtUtc);
    }

    [Fact]
    public void NewSessionClosurePropertiesStartNull()
    {
        var session = CreateOpenSession();

        Assert.Null(session.ClosedByUserId);
        Assert.Null(session.ExpectedCash);
        Assert.Null(session.CountedCash);
        Assert.Null(session.CashDifference);
        Assert.Null(session.ClosedAtUtc);
    }

    [Fact]
    public void RejectsDefaultRegisterSessionId()
    {
        Assert.Throws<DomainValidationException>(
            () => new RegisterSession(default, RegisterId.New(), UserId.New(), new Money(100m, "USD"), OpenedAtUtc));
    }

    [Fact]
    public void RejectsDefaultRegisterId()
    {
        Assert.Throws<DomainValidationException>(
            () => new RegisterSession(RegisterSessionId.New(), default, UserId.New(), new Money(100m, "USD"), OpenedAtUtc));
    }

    [Fact]
    public void RejectsDefaultOpenedByUserId()
    {
        Assert.Throws<DomainValidationException>(
            () => new RegisterSession(RegisterSessionId.New(), RegisterId.New(), default, new Money(100m, "USD"), OpenedAtUtc));
    }

    [Fact]
    public void RejectsNullOpeningFloat()
    {
        Assert.Throws<DomainValidationException>(
            () => new RegisterSession(RegisterSessionId.New(), RegisterId.New(), UserId.New(), null!, OpenedAtUtc));
    }

    [Fact]
    public void RejectsNegativeOpeningFloat()
    {
        Assert.Throws<DomainValidationException>(
            () => new RegisterSession(RegisterSessionId.New(), RegisterId.New(), UserId.New(), new Money(-1m, "USD"), OpenedAtUtc));
    }

    [Fact]
    public void RejectsNonUtcOpenedAtUtc()
    {
        var nonUtc = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.FromHours(-5));

        Assert.Throws<DomainValidationException>(
            () => new RegisterSession(RegisterSessionId.New(), RegisterId.New(), UserId.New(), new Money(100m, "USD"), nonUtc));
    }

    // ---------- Cierre correcto ----------

    [Fact]
    public void ClosesAnOpenSession()
    {
        var session = CreateOpenSession();

        session.Close(UserId.New(), new Money(100m, "USD"), new Money(100m, "USD"), ClosedAtUtc);

        Assert.Equal(RegisterSessionStatus.Closed, session.Status);
    }

    [Fact]
    public void CloseStoresClosedByUserId()
    {
        var session = CreateOpenSession();
        var closedByUserId = UserId.New();

        session.Close(closedByUserId, new Money(100m, "USD"), new Money(100m, "USD"), ClosedAtUtc);

        Assert.Equal(closedByUserId, session.ClosedByUserId);
    }

    [Fact]
    public void CloseStoresExpectedCash()
    {
        var session = CreateOpenSession();
        var expectedCash = new Money(120m, "USD");

        session.Close(UserId.New(), expectedCash, new Money(120m, "USD"), ClosedAtUtc);

        Assert.Equal(expectedCash, session.ExpectedCash);
    }

    [Fact]
    public void CloseStoresCountedCash()
    {
        var session = CreateOpenSession();
        var countedCash = new Money(120m, "USD");

        session.Close(UserId.New(), new Money(120m, "USD"), countedCash, ClosedAtUtc);

        Assert.Equal(countedCash, session.CountedCash);
    }

    [Fact]
    public void CloseStoresClosedAtUtc()
    {
        var session = CreateOpenSession();

        session.Close(UserId.New(), new Money(100m, "USD"), new Money(100m, "USD"), ClosedAtUtc);

        Assert.Equal(ClosedAtUtc, session.ClosedAtUtc);
    }

    [Fact]
    public void CloseCalculatesPositiveDifference()
    {
        var session = CreateOpenSession();

        session.Close(UserId.New(), new Money(100m, "USD"), new Money(110m, "USD"), ClosedAtUtc);

        Assert.Equal(new Money(10m, "USD"), session.CashDifference);
    }

    [Fact]
    public void CloseCalculatesZeroDifference()
    {
        var session = CreateOpenSession();

        session.Close(UserId.New(), new Money(100m, "USD"), new Money(100m, "USD"), ClosedAtUtc);

        Assert.Equal(new Money(0m, "USD"), session.CashDifference);
    }

    [Fact]
    public void CloseCalculatesNegativeDifference()
    {
        var session = CreateOpenSession();

        session.Close(UserId.New(), new Money(100m, "USD"), new Money(90m, "USD"), ClosedAtUtc);

        Assert.Equal(new Money(-10m, "USD"), session.CashDifference);
    }

    [Fact]
    public void AllowsClosedAtUtcEqualToOpenedAtUtc()
    {
        var session = CreateOpenSession();

        session.Close(UserId.New(), new Money(100m, "USD"), new Money(100m, "USD"), OpenedAtUtc);

        Assert.Equal(OpenedAtUtc, session.ClosedAtUtc);
    }

    // ---------- Cierre inválido ----------

    [Fact]
    public void RejectsClosingAnAlreadyClosedSession()
    {
        var session = CreateOpenSession();
        session.Close(UserId.New(), new Money(100m, "USD"), new Money(100m, "USD"), ClosedAtUtc);

        Assert.Throws<DomainValidationException>(
            () => session.Close(UserId.New(), new Money(100m, "USD"), new Money(100m, "USD"), ClosedAtUtc));
    }

    [Fact]
    public void RejectsDefaultUserIdOnClose()
    {
        var session = CreateOpenSession();

        Assert.Throws<DomainValidationException>(
            () => session.Close(default, new Money(100m, "USD"), new Money(100m, "USD"), ClosedAtUtc));
    }

    [Fact]
    public void RejectsNullExpectedCash()
    {
        var session = CreateOpenSession();

        Assert.Throws<DomainValidationException>(
            () => session.Close(UserId.New(), null!, new Money(100m, "USD"), ClosedAtUtc));
    }

    [Fact]
    public void RejectsNullCountedCash()
    {
        var session = CreateOpenSession();

        Assert.Throws<DomainValidationException>(
            () => session.Close(UserId.New(), new Money(100m, "USD"), null!, ClosedAtUtc));
    }

    [Fact]
    public void RejectsNegativeExpectedCash()
    {
        var session = CreateOpenSession();

        Assert.Throws<DomainValidationException>(
            () => session.Close(UserId.New(), new Money(-1m, "USD"), new Money(100m, "USD"), ClosedAtUtc));
    }

    [Fact]
    public void RejectsNegativeCountedCash()
    {
        var session = CreateOpenSession();

        Assert.Throws<DomainValidationException>(
            () => session.Close(UserId.New(), new Money(100m, "USD"), new Money(-1m, "USD"), ClosedAtUtc));
    }

    [Fact]
    public void RejectsDifferentCurrencyInExpectedCash()
    {
        var session = CreateOpenSession(new Money(100m, "USD"));

        Assert.Throws<DomainValidationException>(
            () => session.Close(UserId.New(), new Money(100m, "EUR"), new Money(100m, "USD"), ClosedAtUtc));
    }

    [Fact]
    public void RejectsDifferentCurrencyInCountedCash()
    {
        var session = CreateOpenSession(new Money(100m, "USD"));

        Assert.Throws<DomainValidationException>(
            () => session.Close(UserId.New(), new Money(100m, "USD"), new Money(100m, "EUR"), ClosedAtUtc));
    }

    [Fact]
    public void RejectsNonUtcClosedAtUtc()
    {
        var session = CreateOpenSession();
        var nonUtc = new DateTimeOffset(2026, 1, 1, 20, 0, 0, TimeSpan.FromHours(-5));

        Assert.Throws<DomainValidationException>(
            () => session.Close(UserId.New(), new Money(100m, "USD"), new Money(100m, "USD"), nonUtc));
    }

    [Fact]
    public void RejectsClosedAtUtcBeforeOpenedAtUtc()
    {
        var session = CreateOpenSession();
        var beforeOpening = OpenedAtUtc.AddHours(-1);

        Assert.Throws<DomainValidationException>(
            () => session.Close(UserId.New(), new Money(100m, "USD"), new Money(100m, "USD"), beforeOpening));
    }

    // ---------- Atomicidad ----------

    [Fact]
    public void FailedCloseDueToCurrencyMismatchDoesNotModifySession()
    {
        var session = CreateOpenSession(new Money(100m, "USD"));

        Assert.Throws<DomainValidationException>(
            () => session.Close(UserId.New(), new Money(100m, "EUR"), new Money(100m, "USD"), ClosedAtUtc));

        Assert.Equal(RegisterSessionStatus.Open, session.Status);
        Assert.Null(session.ClosedByUserId);
        Assert.Null(session.ExpectedCash);
        Assert.Null(session.CountedCash);
        Assert.Null(session.CashDifference);
        Assert.Null(session.ClosedAtUtc);
    }

    [Fact]
    public void FailedCloseDueToDateBeforeOpeningDoesNotModifySession()
    {
        var session = CreateOpenSession();
        var beforeOpening = OpenedAtUtc.AddHours(-1);

        Assert.Throws<DomainValidationException>(
            () => session.Close(UserId.New(), new Money(100m, "USD"), new Money(100m, "USD"), beforeOpening));

        Assert.Equal(RegisterSessionStatus.Open, session.Status);
        Assert.Null(session.ClosedByUserId);
        Assert.Null(session.ExpectedCash);
        Assert.Null(session.CountedCash);
        Assert.Null(session.CashDifference);
        Assert.Null(session.ClosedAtUtc);
    }

    // ---------- Rehydrate ----------

    private static RegisterSession RehydrateOpen(
        RegisterSessionId? id = null,
        RegisterId? registerId = null,
        UserId? openedByUserId = null,
        Money? openingFloat = null,
        DateTimeOffset? openedAtUtc = null) =>
        RegisterSession.Rehydrate(
            id ?? RegisterSessionId.New(),
            registerId ?? RegisterId.New(),
            openedByUserId ?? UserId.New(),
            openingFloat ?? new Money(100m, "USD"),
            openedAtUtc ?? OpenedAtUtc,
            RegisterSessionStatus.Open,
            closedByUserId: null,
            expectedCash: null,
            countedCash: null,
            cashDifference: null,
            closedAtUtc: null);

    private static RegisterSession RehydrateClosed(
        Money? cashDifference = null,
        DateTimeOffset? closedAtUtc = null) =>
        RegisterSession.Rehydrate(
            RegisterSessionId.New(),
            RegisterId.New(),
            UserId.New(),
            new Money(100m, "USD"),
            OpenedAtUtc,
            RegisterSessionStatus.Closed,
            UserId.New(),
            new Money(100m, "USD"),
            new Money(110m, "USD"),
            cashDifference ?? new Money(10m, "USD"),
            closedAtUtc ?? ClosedAtUtc);

    [Fact]
    public void RehydrateOpenRestoresIdentifiersAndOpeningFloat()
    {
        var id = RegisterSessionId.New();
        var registerId = RegisterId.New();
        var openedByUserId = UserId.New();
        var openingFloat = new Money(250m, "USD");

        var session = RehydrateOpen(id, registerId, openedByUserId, openingFloat, OpenedAtUtc);

        Assert.Equal(id, session.Id);
        Assert.Equal(registerId, session.RegisterId);
        Assert.Equal(openedByUserId, session.OpenedByUserId);
        Assert.Equal(openingFloat, session.OpeningFloat);
        Assert.Equal(OpenedAtUtc, session.OpenedAtUtc);
        Assert.Equal(RegisterSessionStatus.Open, session.Status);
    }

    [Fact]
    public void RehydrateOpenLeavesClosureDataNull()
    {
        var session = RehydrateOpen();

        Assert.Null(session.ClosedByUserId);
        Assert.Null(session.ExpectedCash);
        Assert.Null(session.CountedCash);
        Assert.Null(session.CashDifference);
        Assert.Null(session.ClosedAtUtc);
    }

    [Fact]
    public void RehydrateOpenRejectsPresentClosureData()
    {
        Assert.Throws<DomainValidationException>(
            () => RegisterSession.Rehydrate(
                RegisterSessionId.New(),
                RegisterId.New(),
                UserId.New(),
                new Money(100m, "USD"),
                OpenedAtUtc,
                RegisterSessionStatus.Open,
                UserId.New(),
                null,
                null,
                null,
                null));
    }

    [Fact]
    public void RehydrateClosedAcceptsPositiveCashDifference()
    {
        var session = RegisterSession.Rehydrate(
            RegisterSessionId.New(),
            RegisterId.New(),
            UserId.New(),
            new Money(100m, "USD"),
            OpenedAtUtc,
            RegisterSessionStatus.Closed,
            UserId.New(),
            new Money(100m, "USD"),
            new Money(110m, "USD"),
            new Money(10m, "USD"),
            ClosedAtUtc);

        Assert.Equal(new Money(10m, "USD"), session.CashDifference);
    }

    [Fact]
    public void RehydrateClosedAcceptsNegativeCashDifference()
    {
        var session = RegisterSession.Rehydrate(
            RegisterSessionId.New(),
            RegisterId.New(),
            UserId.New(),
            new Money(100m, "USD"),
            OpenedAtUtc,
            RegisterSessionStatus.Closed,
            UserId.New(),
            new Money(100m, "USD"),
            new Money(90m, "USD"),
            new Money(-10m, "USD"),
            ClosedAtUtc);

        Assert.Equal(new Money(-10m, "USD"), session.CashDifference);
    }

    [Fact]
    public void RehydrateClosedAcceptsZeroCashDifference()
    {
        var session = RegisterSession.Rehydrate(
            RegisterSessionId.New(),
            RegisterId.New(),
            UserId.New(),
            new Money(100m, "USD"),
            OpenedAtUtc,
            RegisterSessionStatus.Closed,
            UserId.New(),
            new Money(100m, "USD"),
            new Money(100m, "USD"),
            new Money(0m, "USD"),
            ClosedAtUtc);

        Assert.Equal(new Money(0m, "USD"), session.CashDifference);
    }

    [Fact]
    public void RehydrateClosedRejectsCashDifferenceThatDoesNotMatchExpectedAndCountedCash()
    {
        var persistedDifference = new Money(999m, "USD");

        Assert.Throws<DomainValidationException>(
            () => RehydrateClosed(cashDifference: persistedDifference));
    }

    [Fact]
    public void RehydrateClosedRestoresClosureFields()
    {
        var closedByUserId = UserId.New();
        var expectedCash = new Money(100m, "USD");
        var countedCash = new Money(110m, "USD");
        var cashDifference = new Money(10m, "USD");

        var session = RegisterSession.Rehydrate(
            RegisterSessionId.New(),
            RegisterId.New(),
            UserId.New(),
            new Money(100m, "USD"),
            OpenedAtUtc,
            RegisterSessionStatus.Closed,
            closedByUserId,
            expectedCash,
            countedCash,
            cashDifference,
            ClosedAtUtc);

        Assert.Equal(closedByUserId, session.ClosedByUserId);
        Assert.Equal(expectedCash, session.ExpectedCash);
        Assert.Equal(countedCash, session.CountedCash);
        Assert.Equal(cashDifference, session.CashDifference);
        Assert.Equal(ClosedAtUtc, session.ClosedAtUtc);
    }

    [Fact]
    public void RehydratedClosedSessionRejectsClose()
    {
        var session = RehydrateClosed();

        Assert.Throws<DomainValidationException>(
            () => session.Close(UserId.New(), new Money(100m, "USD"), new Money(100m, "USD"), ClosedAtUtc));
    }

    [Fact]
    public void RehydrateClosedRejectsMissingClosedByUserId()
    {
        Assert.Throws<DomainValidationException>(
            () => RegisterSession.Rehydrate(
                RegisterSessionId.New(),
                RegisterId.New(),
                UserId.New(),
                new Money(100m, "USD"),
                OpenedAtUtc,
                RegisterSessionStatus.Closed,
                null,
                new Money(100m, "USD"),
                new Money(110m, "USD"),
                new Money(10m, "USD"),
                ClosedAtUtc));
    }

    [Fact]
    public void RehydrateClosedRejectsMissingExpectedCash()
    {
        Assert.Throws<DomainValidationException>(
            () => RegisterSession.Rehydrate(
                RegisterSessionId.New(),
                RegisterId.New(),
                UserId.New(),
                new Money(100m, "USD"),
                OpenedAtUtc,
                RegisterSessionStatus.Closed,
                UserId.New(),
                null,
                new Money(110m, "USD"),
                new Money(10m, "USD"),
                ClosedAtUtc));
    }

    [Fact]
    public void RehydrateClosedRejectsMissingCountedCash()
    {
        Assert.Throws<DomainValidationException>(
            () => RegisterSession.Rehydrate(
                RegisterSessionId.New(),
                RegisterId.New(),
                UserId.New(),
                new Money(100m, "USD"),
                OpenedAtUtc,
                RegisterSessionStatus.Closed,
                UserId.New(),
                new Money(100m, "USD"),
                null,
                new Money(10m, "USD"),
                ClosedAtUtc));
    }

    [Fact]
    public void RehydrateClosedRejectsMissingCashDifference()
    {
        Assert.Throws<DomainValidationException>(
            () => RegisterSession.Rehydrate(
                RegisterSessionId.New(),
                RegisterId.New(),
                UserId.New(),
                new Money(100m, "USD"),
                OpenedAtUtc,
                RegisterSessionStatus.Closed,
                UserId.New(),
                new Money(100m, "USD"),
                new Money(110m, "USD"),
                null,
                ClosedAtUtc));
    }

    [Fact]
    public void RehydrateClosedRejectsMissingClosedAtUtc()
    {
        Assert.Throws<DomainValidationException>(
            () => RegisterSession.Rehydrate(
                RegisterSessionId.New(),
                RegisterId.New(),
                UserId.New(),
                new Money(100m, "USD"),
                OpenedAtUtc,
                RegisterSessionStatus.Closed,
                UserId.New(),
                new Money(100m, "USD"),
                new Money(110m, "USD"),
                new Money(10m, "USD"),
                null));
    }

    [Fact]
    public void RehydrateClosedRejectsClosedAtUtcBeforeOpenedAtUtc()
    {
        var beforeOpening = OpenedAtUtc.AddHours(-1);

        Assert.Throws<DomainValidationException>(
            () => RegisterSession.Rehydrate(
                RegisterSessionId.New(),
                RegisterId.New(),
                UserId.New(),
                new Money(100m, "USD"),
                OpenedAtUtc,
                RegisterSessionStatus.Closed,
                UserId.New(),
                new Money(100m, "USD"),
                new Money(110m, "USD"),
                new Money(10m, "USD"),
                beforeOpening));
    }

    [Fact]
    public void RehydrateClosedRejectsMismatchedCurrencyInCashDifference()
    {
        Assert.Throws<DomainValidationException>(
            () => RegisterSession.Rehydrate(
                RegisterSessionId.New(),
                RegisterId.New(),
                UserId.New(),
                new Money(100m, "USD"),
                OpenedAtUtc,
                RegisterSessionStatus.Closed,
                UserId.New(),
                new Money(100m, "USD"),
                new Money(110m, "USD"),
                new Money(10m, "EUR"),
                ClosedAtUtc));
    }

    [Fact]
    public void RehydrateRejectsUndefinedStatus()
    {
        Assert.Throws<DomainValidationException>(
            () => RegisterSession.Rehydrate(
                RegisterSessionId.New(),
                RegisterId.New(),
                UserId.New(),
                new Money(100m, "USD"),
                OpenedAtUtc,
                (RegisterSessionStatus)99,
                null,
                null,
                null,
                null,
                null));
    }

    [Fact]
    public void RehydrateRejectsDefaultRegisterSessionId()
    {
        Assert.Throws<DomainValidationException>(
            () => RegisterSession.Rehydrate(
                default,
                RegisterId.New(),
                UserId.New(),
                new Money(100m, "USD"),
                OpenedAtUtc,
                RegisterSessionStatus.Open,
                null,
                null,
                null,
                null,
                null));
    }

    [Fact]
    public void RehydrateRejectsDefaultRegisterId()
    {
        Assert.Throws<DomainValidationException>(
            () => RegisterSession.Rehydrate(
                RegisterSessionId.New(),
                default,
                UserId.New(),
                new Money(100m, "USD"),
                OpenedAtUtc,
                RegisterSessionStatus.Open,
                null,
                null,
                null,
                null,
                null));
    }

    [Fact]
    public void RehydrateRejectsDefaultOpenedByUserId()
    {
        Assert.Throws<DomainValidationException>(
            () => RegisterSession.Rehydrate(
                RegisterSessionId.New(),
                RegisterId.New(),
                default,
                new Money(100m, "USD"),
                OpenedAtUtc,
                RegisterSessionStatus.Open,
                null,
                null,
                null,
                null,
                null));
    }

    [Fact]
    public void RehydrateRejectsNonUtcOpenedAtUtc()
    {
        var nonUtc = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.FromHours(-5));

        Assert.Throws<DomainValidationException>(() => RehydrateOpen(openedAtUtc: nonUtc));
    }
}
