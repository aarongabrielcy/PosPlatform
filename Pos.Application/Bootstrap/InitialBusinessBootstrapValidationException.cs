namespace Pos.Application.Bootstrap;

// Traduce un rechazo de Domain (DomainValidationException) ocurrido durante la construcción de
// los agregados del bootstrap a un tipo propio de Application. Evita que los consumidores de
// IInitialBusinessBootstrapService —incluida la UI de Pos.Desktop— necesiten depender de
// Pos.Domain para distinguir un error de validación de uno inesperado.
public sealed class InitialBusinessBootstrapValidationException : Exception
{
    public InitialBusinessBootstrapValidationException(string message, Exception innerException)
        : base(ValidateMessage(message), innerException)
    {
    }

    private static string ValidateMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException(
                "El mensaje de InitialBusinessBootstrapValidationException no puede estar vacío.", nameof(message));
        }

        return message;
    }
}
