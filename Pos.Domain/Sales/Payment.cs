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

    // Referencia/autorización externa (TAREA 25C/25C-FIX): obligatoria únicamente para Card usado
    // como Manual Card (terminal externa ajena a PosPlatform) y prohibida para Cash. Nunca debe
    // contener PAN/CVV/datos de tarjeta: es solo el número de autorización que el cajero copia de
    // la terminal externa. BankTransfer conserva su comportamiento previo (sin invariante nueva):
    // esta fase de BASIC V1 no define ninguna regla de referencia para BankTransfer ni lo expone en
    // Checkout.
    public string? Reference { get; }

    // Constructor público: crea un pago NUEVO y exige la invariante completa (sección "creation
    // rules"). Usado por Sale.AddPayment, nunca por la reconstrucción de pagos persistidos.
    public Payment(
        PaymentId id,
        PaymentMethod method,
        Money amount,
        DateTimeOffset paidAtUtc,
        string? reference = null)
        : this(id, method, amount, paidAtUtc, reference, isRehydration: false)
    {
    }

    // Reconstruye un pago ya persistido (TAREA 25C-FIX sección 5): a diferencia del constructor
    // público, NUNCA exige que un Card tenga Reference. Esto evita que una fila histórica Card con
    // reference = NULL (proveniente de antes de que la referencia fuera obligatoria) rompa la
    // reconstrucción del agregado Sale. La creación de un Card NUEVO sigue siendo estricta: solo
    // este camino de rehidratación es permisivo.
    public static Payment Rehydrate(
        PaymentId id,
        PaymentMethod method,
        Money amount,
        DateTimeOffset paidAtUtc,
        string? reference) =>
        new(id, method, amount, paidAtUtc, reference, isRehydration: true);

    private Payment(
        PaymentId id,
        PaymentMethod method,
        Money amount,
        DateTimeOffset paidAtUtc,
        string? reference,
        bool isRehydration)
    {
        Id = EnsureNotEmpty(id);
        Method = EnsureDefined(method);
        Amount = EnsurePositive(amount);
        PaidAtUtc = EnsureUtc(paidAtUtc, nameof(paidAtUtc));
        Reference = isRehydration
            ? NormalizeRehydratedReference(Method, reference)
            : EnsureValidReference(Method, reference);
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

    // Invariante de CREACIÓN (TAREA 25C-FIX sección 3-4): la referencia obligatoria es una regla de
    // negocio de Manual Card específicamente, no de "cualquier método distinto de Cash". Card es el
    // único método alcanzable desde Checkout además de Cash en BASIC V1; BankTransfer no se expone
    // en Checkout y no tiene ninguna invariante nueva de Reference en esta fase.
    private static string? EnsureValidReference(PaymentMethod method, string? reference)
    {
        if (method == PaymentMethod.Cash)
        {
            if (reference is not null)
            {
                throw new DomainValidationException("Un pago Cash no puede tener Reference.");
            }

            return null;
        }

        if (method != PaymentMethod.Card)
        {
            // BankTransfer (u otro método futuro sin invariante definida todavía): comportamiento
            // previo preservado, sin exigir ni transformar Reference.
            return reference;
        }

        if (string.IsNullOrWhiteSpace(reference))
        {
            throw new DomainValidationException("Reference es obligatoria para pagos Card (Manual Card).");
        }

        var trimmed = reference.Trim();

        if (trimmed.Length > 100)
        {
            throw new DomainValidationException("Reference no puede superar 100 caracteres.");
        }

        return trimmed;
    }

    // Invariante de REHIDRATACIÓN (TAREA 25C-FIX sección 5): nunca exige presencia de Reference,
    // ni siquiera para Card — solo así una fila histórica Card con reference = NULL (persistida
    // antes de que la referencia fuera obligatoria) puede reconstruirse sin lanzar. Cash conserva su
    // invariante (nunca tuvo Reference en ningún momento de este esquema, no hay caso legado que
    // relajar). Para el resto, solo normaliza espacios en blanco a null; nunca fabrica un valor.
    private static string? NormalizeRehydratedReference(PaymentMethod method, string? reference)
    {
        if (method == PaymentMethod.Cash)
        {
            if (reference is not null)
            {
                throw new DomainValidationException("Un pago Cash no puede tener Reference.");
            }

            return null;
        }

        return string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
    }
}
