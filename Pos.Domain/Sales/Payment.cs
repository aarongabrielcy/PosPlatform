using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Common.ValueObjects;

namespace Pos.Domain.Sales;

public sealed class Payment
{
    public PaymentId Id { get; }

    public PaymentMethod Method { get; }

    public Money Amount { get; }

    public DateTimeOffset PaidAtUtc { get; }

    public Payment(
        PaymentId id,
        PaymentMethod method,
        Money amount,
        DateTimeOffset paidAtUtc)
    {
        Id = EnsureNotEmpty(id);
        Method = EnsureDefined(method);
        Amount = EnsurePositive(amount);
        PaidAtUtc = EnsureUtc(paidAtUtc, nameof(paidAtUtc));
    }

    private static PaymentId EnsureNotEmpty(PaymentId id)
    {
        if (id.Value == Guid.Empty)
        {
            throw new DomainValidationException("Id no puede ser vacío.");
        }

        return id;
    }

    private static PaymentMethod EnsureDefined(PaymentMethod method)
    {
        if (!Enum.IsDefined(method))
        {
            throw new DomainValidationException("Method no es un valor válido de PaymentMethod.");
        }

        return method;
    }

    private static Money EnsurePositive(Money amount)
    {
        if (amount is null)
        {
            throw new DomainValidationException("Amount es obligatorio.");
        }

        if (amount.Amount <= 0m)
        {
            throw new DomainValidationException("Amount debe ser mayor que cero.");
        }

        return amount;
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
