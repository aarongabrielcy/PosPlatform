namespace Pos.Domain.Common.Exceptions;

public sealed class DomainValidationException : Exception
{
    public DomainValidationException(string message)
        : base(ValidateMessage(message))
    {
    }

    private static string ValidateMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("El mensaje de DomainValidationException no puede estar vacío.", nameof(message));
        }

        return message;
    }
}
