using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Common.ValueObjects;
using Pos.Domain.Sales;

namespace Pos.Domain.Tests.Sales;

public class PaymentTests
{
    private static readonly DateTimeOffset PaidAtUtc = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    private static Payment CreatePayment(
        PaymentId? id = null,
        PaymentMethod method = PaymentMethod.Cash,
        Money? amount = null,
        DateTimeOffset? paidAtUtc = null,
        string? reference = null) =>
        new(
            id ?? PaymentId.New(),
            method,
            amount ?? new Money(10m, "MXN"),
            paidAtUtc ?? PaidAtUtc,
            reference ?? (method == PaymentMethod.Cash ? null : "AUTH-0001"));

    [Fact]
    public void PreservesId()
    {
        var id = PaymentId.New();

        var payment = CreatePayment(id: id);

        Assert.Equal(id, payment.Id);
    }

    [Fact]
    public void PreservesMethod()
    {
        var payment = CreatePayment(method: PaymentMethod.Card);

        Assert.Equal(PaymentMethod.Card, payment.Method);
    }

    [Fact]
    public void PreservesAmount()
    {
        var amount = new Money(25m, "MXN");

        var payment = CreatePayment(amount: amount);

        Assert.Equal(amount, payment.Amount);
    }

    [Fact]
    public void PreservesPaidAtUtc()
    {
        var payment = CreatePayment(paidAtUtc: PaidAtUtc);

        Assert.Equal(PaidAtUtc, payment.PaidAtUtc);
    }

    [Fact]
    public void RejectsDefaultPaymentId()
    {
        Assert.Throws<DomainValidationException>(() => new Payment(
            default, PaymentMethod.Cash, new Money(10m, "MXN"), PaidAtUtc));
    }

    [Fact]
    public void RejectsUndefinedEnumValue()
    {
        Assert.Throws<DomainValidationException>(() => new Payment(
            PaymentId.New(), (PaymentMethod)999, new Money(10m, "MXN"), PaidAtUtc));
    }

    [Fact]
    public void RejectsNullAmount()
    {
        Assert.Throws<DomainValidationException>(() => new Payment(
            PaymentId.New(), PaymentMethod.Cash, null!, PaidAtUtc));
    }

    [Fact]
    public void RejectsZeroAmount()
    {
        Assert.Throws<DomainValidationException>(() => CreatePayment(amount: new Money(0m, "MXN")));
    }

    [Fact]
    public void RejectsNegativeAmount()
    {
        Assert.Throws<DomainValidationException>(() => CreatePayment(amount: new Money(-1m, "MXN")));
    }

    [Fact]
    public void RejectsNonUtcPaidAtUtc()
    {
        var nonUtc = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.FromHours(-5));

        Assert.Throws<DomainValidationException>(() => CreatePayment(paidAtUtc: nonUtc));
    }

    // ---------- Reference (TAREA 25C: Manual Card) ----------

    [Fact]
    public void CashPaymentHasNullReference()
    {
        var payment = CreatePayment(method: PaymentMethod.Cash);

        Assert.Null(payment.Reference);
    }

    [Fact]
    public void CashPaymentRejectsAnyReference()
    {
        Assert.Throws<DomainValidationException>(() => CreatePayment(method: PaymentMethod.Cash, reference: "AUTH-1"));
    }

    [Fact]
    public void CardPaymentPreservesTrimmedReference()
    {
        var payment = CreatePayment(method: PaymentMethod.Card, reference: "  AUTH-9001  ");

        Assert.Equal("AUTH-9001", payment.Reference);
    }

    [Fact]
    public void CardPaymentRejectsNullReference()
    {
        Assert.Throws<DomainValidationException>(() => new Payment(
            PaymentId.New(), PaymentMethod.Card, new Money(10m, "MXN"), PaidAtUtc, null));
    }

    [Fact]
    public void CardPaymentRejectsEmptyReference()
    {
        Assert.Throws<DomainValidationException>(() => CreatePayment(method: PaymentMethod.Card, reference: string.Empty));
    }

    [Fact]
    public void CardPaymentRejectsWhitespaceOnlyReference()
    {
        Assert.Throws<DomainValidationException>(() => CreatePayment(method: PaymentMethod.Card, reference: "   "));
    }

    [Fact]
    public void CardPaymentRejectsReferenceLongerThan100Characters()
    {
        var tooLong = new string('A', 101);

        Assert.Throws<DomainValidationException>(() => CreatePayment(method: PaymentMethod.Card, reference: tooLong));
    }

    // ---------- BankTransfer (TAREA 25C-FIX sección 3-4): sin invariante nueva de Reference ----------

    [Fact]
    public void BankTransferPaymentCreationAcceptsNullReference()
    {
        var payment = new Payment(
            PaymentId.New(), PaymentMethod.BankTransfer, new Money(10m, "MXN"), PaidAtUtc, null);

        Assert.Null(payment.Reference);
    }

    [Fact]
    public void BankTransferPaymentCreationAcceptsAnyNonNullReferenceWithoutTrimming()
    {
        var payment = new Payment(
            PaymentId.New(), PaymentMethod.BankTransfer, new Money(10m, "MXN"), PaidAtUtc, "  REF-1  ");

        // Sin invariante nueva: no se recorta ni se valida, a diferencia de Card.
        Assert.Equal("  REF-1  ", payment.Reference);
    }

    // ---------- Payment.Rehydrate (TAREA 25C-FIX sección 5-6): compatibilidad con filas históricas ----------

    [Fact]
    public void RehydrateAcceptsHistoricalCardPaymentWithNullReference()
    {
        var payment = Payment.Rehydrate(
            PaymentId.New(), PaymentMethod.Card, new Money(10m, "MXN"), PaidAtUtc, null);

        Assert.Equal(PaymentMethod.Card, payment.Method);
        Assert.Null(payment.Reference);
    }

    [Fact]
    public void RehydrateAcceptsHistoricalBankTransferPaymentWithNullReference()
    {
        var payment = Payment.Rehydrate(
            PaymentId.New(), PaymentMethod.BankTransfer, new Money(10m, "MXN"), PaidAtUtc, null);

        Assert.Equal(PaymentMethod.BankTransfer, payment.Method);
        Assert.Null(payment.Reference);
    }

    [Fact]
    public void RehydrateTrimsAWhitespaceOnlyNonCashReferenceToNull()
    {
        var payment = Payment.Rehydrate(
            PaymentId.New(), PaymentMethod.Card, new Money(10m, "MXN"), PaidAtUtc, "   ");

        Assert.Null(payment.Reference);
    }

    [Fact]
    public void RehydratePreservesAPresentCardReference()
    {
        var payment = Payment.Rehydrate(
            PaymentId.New(), PaymentMethod.Card, new Money(10m, "MXN"), PaidAtUtc, "  AUTH-5001  ");

        Assert.Equal("AUTH-5001", payment.Reference);
    }

    [Fact]
    public void RehydrateStillRejectsACashPaymentWithANonNullReference()
    {
        Assert.Throws<DomainValidationException>(() => Payment.Rehydrate(
            PaymentId.New(), PaymentMethod.Cash, new Money(10m, "MXN"), PaidAtUtc, "AUTH-1"));
    }

    // Distingue las dos rutas: crear un Card NUEVO sigue siendo estricto (exige Reference), aunque
    // Rehydrate acepte null para el mismo Method (TAREA 25C-FIX sección 6, "different concerns").
    [Fact]
    public void NewCardPaymentCreationStillRejectsNullReferenceEvenThoughRehydrateAcceptsIt()
    {
        Assert.Throws<DomainValidationException>(() => new Payment(
            PaymentId.New(), PaymentMethod.Card, new Money(10m, "MXN"), PaidAtUtc, null));

        var rehydrated = Payment.Rehydrate(
            PaymentId.New(), PaymentMethod.Card, new Money(10m, "MXN"), PaidAtUtc, null);

        Assert.Null(rehydrated.Reference);
    }
}
