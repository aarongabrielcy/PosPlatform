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
}
