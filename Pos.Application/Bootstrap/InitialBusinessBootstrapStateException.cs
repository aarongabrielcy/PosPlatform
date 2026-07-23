namespace Pos.Application.Bootstrap;

public sealed class InitialBusinessBootstrapStateException : Exception
{
    public InitialBusinessBootstrapStateException(string message)
        : base(ValidateMessage(message))
    {
    }

    public InitialBusinessBootstrapStateException(string message, Exception innerException)
        : base(ValidateMessage(message), innerException)
    {
    }

    private static string ValidateMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException(
                "El mensaje de InitialBusinessBootstrapStateException no puede estar vacío.", nameof(message));
        }

        return message;
    }
}
