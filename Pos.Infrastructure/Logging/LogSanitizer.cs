using System.Text.RegularExpressions;

namespace Pos.Infrastructure.Logging;

// Red de seguridad de defensa en profundidad (BASIC-REL-01, sección 18/48): el código de la
// aplicación ya evita deliberadamente registrar la Installation Credential, encabezados de
// autorización, contraseñas o datos de pago (verificado por auditoría de todos los LoggerMessage
// existentes). Este sanitizador es la última línea de defensa por si un mensaje futuro, o el
// contenido de una excepción de terceros (p. ej. una URL con una cadena de consulta sensible),
// terminara incluyendo un patrón reconocible. Nunca reemplaza la disciplina de no loguear secretos
// en primer lugar.
public static partial class LogSanitizer
{
    // Orden deliberado: BearerTokenPattern debe aplicarse antes que AuthorizationHeaderPattern.
    // AuthorizationHeaderPattern solo consume un token (\S+) tras "Authorization:" — con un valor
    // "Authorization: Bearer <token>", si se aplicara primero solo consumiría la palabra "Bearer" y
    // dejaría el token real sin redactar. Aplicando primero el patrón de Bearer, el token queda
    // redactado sin importar qué encabezado lo precediera.
    private static readonly (Regex Pattern, string Replacement)[] Rules =
    [
        (BearerTokenPattern(), "$1[REDACTED]"),
        (AuthorizationHeaderPattern(), "$1[REDACTED]"),
        (PasswordFieldPattern(), "$1[REDACTED]"),
        (CredentialFieldPattern(), "$1[REDACTED]"),
        (EnrollmentCodeFieldPattern(), "$1[REDACTED]"),
    ];

    public static string Redact(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return message;
        }

        var result = message;

        foreach (var (pattern, replacement) in Rules)
        {
            result = pattern.Replace(result, replacement);
        }

        return result;
    }

    [GeneratedRegex("""(?i)(authorization\s*[:=]\s*)\S+""")]
    private static partial Regex AuthorizationHeaderPattern();

    [GeneratedRegex("""(?i)(bearer\s+)\S+""")]
    private static partial Regex BearerTokenPattern();

    [GeneratedRegex("""(?i)("?password"?\s*[:=]\s*"?)[^",\s]+""")]
    private static partial Regex PasswordFieldPattern();

    [GeneratedRegex("""(?i)("?(?:installation[\s_]?)?credential"?\s*[:=]\s*"?)[^",\s]+""")]
    private static partial Regex CredentialFieldPattern();

    [GeneratedRegex("""(?i)("?enrollment[\s_]?code"?\s*[:=]\s*"?)[^",\s]+""")]
    private static partial Regex EnrollmentCodeFieldPattern();
}
