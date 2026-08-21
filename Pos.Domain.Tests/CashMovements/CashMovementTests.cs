using Pos.Domain.CashMovements;
using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Common.ValueObjects;

namespace Pos.Domain.Tests.CashMovements;

public class CashMovementTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    // ---------- CashIn válido ----------

    [Fact]
    public void CreateCashInSucceedsWithValidData()
    {
        var id = CashMovementId.New();
        var registerSessionId = RegisterSessionId.New();
        var actorUserId = UserId.New();
        var amount = new Money(100m, "MXN");

        var movement = CashMovement.CreateCashIn(id, registerSessionId, actorUserId, amount, "Cambio inicial", CreatedAtUtc);

        Assert.Equal(id, movement.Id);
        Assert.Equal(registerSessionId, movement.RegisterSessionId);
        Assert.Equal(actorUserId, movement.ActorUserId);
        Assert.Equal(CashMovementType.CashIn, movement.Type);
        Assert.Equal(amount, movement.Amount);
        Assert.Equal("Cambio inicial", movement.Reason);
        Assert.Equal(CreatedAtUtc, movement.CreatedAtUtc);
    }

    // ---------- CashOut válido ----------

    [Fact]
    public void CreateCashOutSucceedsWithValidData()
    {
        var movement = CashMovement.CreateCashOut(
            CashMovementId.New(), RegisterSessionId.New(), UserId.New(), new Money(50m, "MXN"),
            "Pago de mensajería", CreatedAtUtc);

        Assert.Equal(CashMovementType.CashOut, movement.Type);
        Assert.Equal(50m, movement.Amount.Amount);
    }

    // ---------- Monto cero rechazado ----------

    [Fact]
    public void CreateCashInRejectsZeroAmount()
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            CashMovement.CreateCashIn(
                CashMovementId.New(), RegisterSessionId.New(), UserId.New(), new Money(0m, "MXN"),
                "Motivo válido", CreatedAtUtc));

        Assert.Contains("Amount", exception.Message);
    }

    // ---------- Monto negativo rechazado ----------

    [Fact]
    public void CreateCashOutRejectsNegativeAmount()
    {
        Assert.Throws<DomainValidationException>(() =>
            CashMovement.CreateCashOut(
                CashMovementId.New(), RegisterSessionId.New(), UserId.New(), new Money(-10m, "MXN"),
                "Motivo válido", CreatedAtUtc));
    }

    // ---------- Reason en blanco rechazado ----------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void CreateCashInRejectsBlankReason(string? reason)
    {
        Assert.Throws<DomainValidationException>(() =>
            CashMovement.CreateCashIn(
                CashMovementId.New(), RegisterSessionId.New(), UserId.New(), new Money(10m, "MXN"),
                reason!, CreatedAtUtc));
    }

    [Fact]
    public void CreateCashInRejectsReasonLongerThan200Characters()
    {
        var tooLong = new string('a', 201);

        Assert.Throws<DomainValidationException>(() =>
            CashMovement.CreateCashIn(
                CashMovementId.New(), RegisterSessionId.New(), UserId.New(), new Money(10m, "MXN"),
                tooLong, CreatedAtUtc));
    }

    [Fact]
    public void CreateCashInAcceptsReasonAtMaxLength()
    {
        var maxLength = new string('a', 200);

        var movement = CashMovement.CreateCashIn(
            CashMovementId.New(), RegisterSessionId.New(), UserId.New(), new Money(10m, "MXN"),
            maxLength, CreatedAtUtc);

        Assert.Equal(200, movement.Reason.Length);
    }

    // ---------- Normalización de Reason ----------

    [Fact]
    public void ReasonIsTrimmed()
    {
        var movement = CashMovement.CreateCashIn(
            CashMovementId.New(), RegisterSessionId.New(), UserId.New(), new Money(10m, "MXN"),
            "  Reposición de efectivo  ", CreatedAtUtc);

        Assert.Equal("Reposición de efectivo", movement.Reason);
    }

    // ---------- Identidad preservada ----------

    [Fact]
    public void RejectsEmptyId()
    {
        Assert.Throws<DomainValidationException>(() =>
            CashMovement.CreateCashIn(
                new CashMovementId(Guid.Empty), RegisterSessionId.New(), UserId.New(), new Money(10m, "MXN"),
                "Motivo", CreatedAtUtc));
    }

    [Fact]
    public void RejectsEmptyRegisterSessionId()
    {
        Assert.Throws<DomainValidationException>(() =>
            CashMovement.CreateCashIn(
                CashMovementId.New(), new RegisterSessionId(Guid.Empty), UserId.New(), new Money(10m, "MXN"),
                "Motivo", CreatedAtUtc));
    }

    [Fact]
    public void RejectsEmptyActorUserId()
    {
        Assert.Throws<DomainValidationException>(() =>
            CashMovement.CreateCashIn(
                CashMovementId.New(), RegisterSessionId.New(), new UserId(Guid.Empty), new Money(10m, "MXN"),
                "Motivo", CreatedAtUtc));
    }

    // ---------- CreatedAtUtc debe ser UTC ----------

    [Fact]
    public void RejectsNonUtcCreatedAt()
    {
        var nonUtc = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.FromHours(-6));

        Assert.Throws<DomainValidationException>(() =>
            CashMovement.CreateCashIn(
                CashMovementId.New(), RegisterSessionId.New(), UserId.New(), new Money(10m, "MXN"),
                "Motivo", nonUtc));
    }

    // ---------- Type se preserva según el factory usado ----------

    [Fact]
    public void TypeIsPreservedPerFactory()
    {
        var cashIn = CashMovement.CreateCashIn(
            CashMovementId.New(), RegisterSessionId.New(), UserId.New(), new Money(10m, "MXN"), "Motivo", CreatedAtUtc);
        var cashOut = CashMovement.CreateCashOut(
            CashMovementId.New(), RegisterSessionId.New(), UserId.New(), new Money(10m, "MXN"), "Motivo", CreatedAtUtc);

        Assert.Equal(CashMovementType.CashIn, cashIn.Type);
        Assert.Equal(CashMovementType.CashOut, cashOut.Type);
    }
}
