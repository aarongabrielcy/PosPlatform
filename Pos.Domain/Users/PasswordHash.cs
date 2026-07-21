using Pos.Domain.Common.Exceptions;

namespace Pos.Domain.Users;

public readonly record struct PasswordHash
{
    public const int MaxLength = 512;

    public string Value { get; }

    public PasswordHash(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException("PasswordHash es obligatorio.");
        }

        if (value.Length > MaxLength)
        {
            throw new DomainValidationException($"PasswordHash no puede exceder {MaxLength} caracteres.");
        }

        Value = value;
    }

    // No se expone el hash a través de ToString para evitar filtrarlo en logs o depuración.
    public override string ToString() => "PasswordHash[REDACTED]";
}
