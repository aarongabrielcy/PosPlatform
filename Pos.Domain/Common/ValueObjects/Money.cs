using System.Globalization;
using Pos.Domain.Common.Exceptions;

namespace Pos.Domain.Common.ValueObjects;

public sealed record Money
{
    public decimal Amount { get; }

    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        Currency = NormalizeCurrency(currency);
        Amount = Round(amount);
    }

    public static Money Zero(string currency) => new(0m, currency);

    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount - right.Amount, left.Currency);
    }

    public static Money operator *(Money money, decimal factor) => new(money.Amount * factor, money.Currency);

    public override string ToString() =>
        $"{Amount.ToString("F2", CultureInfo.InvariantCulture)} {Currency}";

    private static void EnsureSameCurrency(Money left, Money right)
    {
        if (left.Currency != right.Currency)
        {
            throw new DomainValidationException(
                $"No se pueden operar montos con monedas diferentes: {left.Currency} y {right.Currency}.");
        }
    }

    private static decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    private static string NormalizeCurrency(string currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new DomainValidationException("Currency es obligatoria.");
        }

        var normalized = currency.ToUpperInvariant();

        if (normalized.Length != 3 || !normalized.All(c => c is >= 'A' and <= 'Z'))
        {
            throw new DomainValidationException("Currency debe tener exactamente 3 letras (A-Z).");
        }

        return normalized;
    }
}
