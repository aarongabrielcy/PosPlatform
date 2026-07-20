using System.Globalization;
using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.ValueObjects;

namespace Pos.Domain.Tests.Common.ValueObjects;

public class MoneyTests
{
    [Fact]
    public void NormalizesCurrencyToUpperCase()
    {
        var money = new Money(10m, "usd");

        Assert.Equal("USD", money.Currency);
    }

    [Fact]
    public void RejectsNullCurrency()
    {
        Assert.Throws<DomainValidationException>(() => new Money(10m, null!));
    }

    [Fact]
    public void RejectsEmptyCurrency()
    {
        Assert.Throws<DomainValidationException>(() => new Money(10m, string.Empty));
    }

    [Fact]
    public void RejectsInvalidCurrency()
    {
        Assert.Throws<DomainValidationException>(() => new Money(10m, "US1"));
    }

    [Fact]
    public void RoundsAwayFromZeroToTwoDecimals()
    {
        var money = new Money(10.125m, "USD");

        Assert.Equal(10.13m, money.Amount);
    }

    [Fact]
    public void AddsSameCurrency()
    {
        var first = new Money(10m, "USD");
        var second = new Money(5m, "USD");

        var result = first + second;

        Assert.Equal(new Money(15m, "USD"), result);
    }

    [Fact]
    public void SubtractsSameCurrency()
    {
        var first = new Money(10m, "USD");
        var second = new Money(5m, "USD");

        var result = first - second;

        Assert.Equal(new Money(5m, "USD"), result);
    }

    [Fact]
    public void MultipliesByDecimal()
    {
        var money = new Money(10m, "USD");

        var result = money * 3m;

        Assert.Equal(new Money(30m, "USD"), result);
    }

    [Fact]
    public void RejectsAdditionOfDifferentCurrencies()
    {
        var first = new Money(10m, "USD");
        var second = new Money(5m, "EUR");

        Assert.Throws<DomainValidationException>(() => first + second);
    }

    [Fact]
    public void RejectsSubtractionOfDifferentCurrencies()
    {
        var first = new Money(10m, "USD");
        var second = new Money(5m, "EUR");

        Assert.Throws<DomainValidationException>(() => first - second);
    }

    [Fact]
    public void ZeroCreatesZeroAmount()
    {
        var money = Money.Zero("USD");

        Assert.Equal(0m, money.Amount);
        Assert.Equal("USD", money.Currency);
    }

    [Fact]
    public void ToStringIncludesAmountWithTwoDecimalsAndCurrency()
    {
        var money = new Money(10m, "USD");

        Assert.Equal("10.00 USD", money.ToString());
    }

    [Fact]
    public void ToStringIsCultureInvariant()
    {
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("es-ES");

            var money = new Money(10m, "USD");

            Assert.Equal("10.00 USD", money.ToString());
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}
