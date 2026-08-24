using Microsoft.Extensions.Logging;
using Pos.Infrastructure.Logging;

namespace Pos.Infrastructure.Tests.Logging;

// BASIC-REL-01, sección 12-15/48: todas las pruebas usan un directorio temporal dedicado, nunca el
// LocalAppData real de la máquina que ejecuta las pruebas.
//
// Usa [LoggerMessage] para las mismas escrituras de prueba (igual convención que el resto del
// código de producción) en vez de las extensiones LogInformation/LogWarning/LogCritical, que
// disparan CA1848 en este proyecto (AnalysisLevel 8.0-recommended, ver Directory.Build.props).
public partial class RotatingFileLoggerProviderTests
{
    private static string CreateTempLogDirectory() =>
        Path.Combine(Path.GetTempPath(), "PosPlatformLoggingTests_" + Guid.NewGuid());

    // File.ReadAllText abre con FileShare.Read por defecto, incompatible con el archivo activo
    // todavía abierto para escritura por un RotatingFileLoggerProvider vivo (FileShare.ReadWrite) —
    // Windows exige que el acceso ya concedido al escritor (Write) sea compatible con el share
    // solicitado por el nuevo lector, no solo al revés. Un lector real (soporte técnico inspeccionando
    // el registro mientras la app sigue corriendo, sección 22) necesita declarar el mismo
    // FileShare.ReadWrite para poder leer sin cerrar la aplicación primero.
    private static string ReadLogFileWhileStillOpenForWriting(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [Fact]
    public void ConstructorDoesNotCreateTheLogDirectory()
    {
        var directory = CreateTempLogDirectory();

        using var provider = new RotatingFileLoggerProvider(directory);

        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public void FirstLogWriteCreatesTheDirectoryAndActiveFile()
    {
        var directory = CreateTempLogDirectory();
        try
        {
            using (var provider = new RotatingFileLoggerProvider(directory))
            {
                LogApplicationStarted(provider.CreateLogger("Test"));
            }

            Assert.True(Directory.Exists(directory));
            Assert.True(File.Exists(Path.Combine(directory, "posplatform.log")));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void WarningAndAboveAreFlushedImmediatelyEvenWithoutDisposing()
    {
        var directory = CreateTempLogDirectory();
        try
        {
            using var provider = new RotatingFileLoggerProvider(directory);

            LogPrinterUnavailable(provider.CreateLogger("Test"));

            var content = ReadLogFileWhileStillOpenForWriting(Path.Combine(directory, "posplatform.log"));
            Assert.Contains("Impresora no disponible.", content, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void DisposeFlushesBufferedInformationEntries()
    {
        var directory = CreateTempLogDirectory();
        try
        {
            using (var provider = new RotatingFileLoggerProvider(directory))
            {
                LogInformationalEvent(provider.CreateLogger("Test"));
            }

            var content = File.ReadAllText(Path.Combine(directory, "posplatform.log"));
            Assert.Contains("Evento informativo sin nivel de advertencia.", content, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    // Rotación determinística por tamaño (sección 13): fuerza un archivo activo pequeño para no
    // depender de escribir 5 MiB reales en una prueba.
    [Fact]
    public void ExceedingMaxFileSizeRotatesTheActiveFileToRotatedSlotOne()
    {
        var directory = CreateTempLogDirectory();
        try
        {
            using var provider = new RotatingFileLoggerProvider(directory, maxFileSizeBytes: 200, maxRotatedFiles: 9);
            var logger = provider.CreateLogger("Test");

            for (var i = 0; i < 20; i++)
            {
                LogRotationForcingLine(logger, i);
            }

            Assert.True(File.Exists(Path.Combine(directory, "posplatform.log")));
            Assert.True(File.Exists(Path.Combine(directory, "posplatform.1.log")));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    // Retención acotada (sección 13): con maxRotatedFiles=2, nunca deben existir más de 2 archivos
    // rotados; el más antiguo se descarta.
    [Fact]
    public void RetentionNeverKeepsMoreThanTheConfiguredNumberOfRotatedFiles()
    {
        var directory = CreateTempLogDirectory();
        try
        {
            using var provider = new RotatingFileLoggerProvider(directory, maxFileSizeBytes: 100, maxRotatedFiles: 2);
            var logger = provider.CreateLogger("Test");

            for (var i = 0; i < 100; i++)
            {
                LogRetentionForcingLine(logger, i);
            }

            Assert.True(File.Exists(Path.Combine(directory, "posplatform.log")));
            Assert.True(File.Exists(Path.Combine(directory, "posplatform.1.log")));
            Assert.True(File.Exists(Path.Combine(directory, "posplatform.2.log")));
            Assert.False(File.Exists(Path.Combine(directory, "posplatform.3.log")));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    // Reinicio del proceso (sección 48 "restart/reopen behavior"): un archivo activo preexistente de
    // una sesión anterior se conserva y se continúa (append), nunca se sobrescribe.
    [Fact]
    public void ReopeningAfterARestartAppendsToTheExistingActiveFileInsteadOfOverwritingIt()
    {
        var directory = CreateTempLogDirectory();
        try
        {
            using (var firstSession = new RotatingFileLoggerProvider(directory))
            {
                LogFirstSessionEntry(firstSession.CreateLogger("Test"));
            }

            using (var secondSession = new RotatingFileLoggerProvider(directory))
            {
                LogSecondSessionEntry(secondSession.CreateLogger("Test"));
            }

            var content = File.ReadAllText(Path.Combine(directory, "posplatform.log"));
            Assert.Contains("Entrada de la primera sesión.", content, StringComparison.Ordinal);
            Assert.Contains("Entrada de la segunda sesión.", content, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    // Sección 18/48: regresión de extremo a extremo a través del logger real, no solo del
    // sanitizador en aislamiento.
    [Fact]
    public void NeverWritesAnAuthorizationHeaderValueToDisk()
    {
        var directory = CreateTempLogDirectory();
        try
        {
            using (var provider = new RotatingFileLoggerProvider(directory))
            {
                LogFailureWithLeakedAuthorizationHeader(provider.CreateLogger("Test"));
            }

            var content = File.ReadAllText(Path.Combine(directory, "posplatform.log"));
            Assert.DoesNotContain("cred-1.super-secret-value", content, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void LoggingNeverThrowsEvenWhenTheDirectoryCannotBeCreated()
    {
        // Una ruta con un carácter inválido en Windows nunca podrá crearse: prueba que un fallo de
        // E/S al escribir el registro se ignora (best-effort, sección 15/48) en vez de propagarse al
        // código que originó el log.
        var invalidDirectory = Path.Combine(Path.GetTempPath(), "PosPlatformLoggingTests_" + Guid.NewGuid(), "inva|id");

        using var provider = new RotatingFileLoggerProvider(invalidDirectory);
        var logger = provider.CreateLogger("Test");

        var exception = Record.Exception(() => LogNeverThrowingCriticalMessage(logger));

        Assert.Null(exception);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Aplicación iniciada.")]
    private static partial void LogApplicationStarted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Impresora no disponible.")]
    private static partial void LogPrinterUnavailable(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Evento informativo sin nivel de advertencia.")]
    private static partial void LogInformationalEvent(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Línea de prueba número {Index} para forzar la rotación por tamaño.")]
    private static partial void LogRotationForcingLine(ILogger logger, int index);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Línea {Index} de un mensaje suficientemente largo para forzar varias rotaciones.")]
    private static partial void LogRetentionForcingLine(ILogger logger, int index);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Entrada de la primera sesión.")]
    private static partial void LogFirstSessionEntry(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Entrada de la segunda sesión.")]
    private static partial void LogSecondSessionEntry(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Fallo de red. Authorization: Bearer cred-1.super-secret-value")]
    private static partial void LogFailureWithLeakedAuthorizationHeader(ILogger logger);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Este mensaje nunca debe hacer explotar al llamador.")]
    private static partial void LogNeverThrowingCriticalMessage(ILogger logger);
}
