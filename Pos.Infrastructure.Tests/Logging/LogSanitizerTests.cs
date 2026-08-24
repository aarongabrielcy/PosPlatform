using Pos.Infrastructure.Logging;

namespace Pos.Infrastructure.Tests.Logging;

// BASIC-REL-01, sección 18/48: regresión de seguridad de secretos. La aplicación ya evita
// deliberadamente registrar estos valores (verificado por auditoría de todos los LoggerMessage
// existentes) — este sanitizador es una red de seguridad adicional, no la única defensa.
public class LogSanitizerTests
{
    [Theory]
    [InlineData(
        "Authorization: Bearer cred-1.super-secret-value",
        "cred-1.super-secret-value")]
    [InlineData(
        "request.Headers.Authorization = Bearer abcdef123456",
        "abcdef123456")]
    public void RedactsAuthorizationHeaderValues(string message, string secret)
    {
        var redacted = LogSanitizer.Redact(message);

        Assert.DoesNotContain(secret, redacted, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void RedactsPasswordFields()
    {
        var redacted = LogSanitizer.Redact("""{"username":"admin","password":"hunter2secret"}""");

        Assert.DoesNotContain("hunter2secret", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void RedactsInstallationCredentialFields()
    {
        var redacted = LogSanitizer.Redact("InstallationCredential=cred-1.super-secret-value");

        Assert.DoesNotContain("cred-1.super-secret-value", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void RedactsEnrollmentCodeFields()
    {
        var redacted = LogSanitizer.Redact("""{"enrollmentCode":"ABC-123-XYZ"}""");

        Assert.DoesNotContain("ABC-123-XYZ", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void LeavesOrdinaryDiagnosticMessagesUnchanged()
    {
        const string message = "Heartbeat de instalación completado. Resultado=Success, duración=42ms.";

        var redacted = LogSanitizer.Redact(message);

        Assert.Equal(message, redacted);
    }

    [Fact]
    public void HandlesEmptyMessageWithoutThrowing()
    {
        var redacted = LogSanitizer.Redact(string.Empty);

        Assert.Equal(string.Empty, redacted);
    }
}
