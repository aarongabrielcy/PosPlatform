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
        DateTimeOffset? paidAtUtc = null) =>
        new(
            id ?? PaymentId.New(),
            method,
            amount ?? new Money(10m, "MXN"),
            paidAtUtc ?? PaidAtUtc);

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
}
