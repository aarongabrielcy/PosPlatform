using Pos.Domain.Common.Exceptions;

namespace Pos.Domain.Products;

public readonly record struct Sku
{
    public string Value { get; }

    public Sku(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException("Sku es obligatorio.");
        }

        var normalized = value.Trim().ToUpperInvariant();

        if (normalized.Length is < 2 or > 40)
        {
            throw new DomainValidationException("Sku debe tener entre 2 y 40 caracteres.");
        }

        if (!normalized.All(c => c is (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '-' or '_' or '.'))
        {
            throw new DomainValidationException(
                "Sku solo puede contener letras A-Z, números 0-9, guion medio, guion bajo y punto.");
        }

        Value = normalized;
    }

    public override string ToString() => Value;
}
