using Pos.Domain.Common.Exceptions;

namespace Pos.Domain.Products;

public readonly record struct Barcode
{
    public string Value { get; }

    public Barcode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException("Barcode es obligatorio.");
        }

        var normalized = value.Trim();

        if (normalized.Length is < 4 or > 32)
        {
            throw new DomainValidationException("Barcode debe tener entre 4 y 32 caracteres.");
        }

        if (!normalized.All(c => c is >= '0' and <= '9'))
        {
            throw new DomainValidationException("Barcode solo puede contener dígitos 0-9.");
        }

        Value = normalized;
    }

    public override string ToString() => Value;
}
