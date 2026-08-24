using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Pos.Infrastructure.Logging;

internal sealed class RotatingFileLogger : ILogger
{
    private readonly RotatingFileLoggerProvider _provider;
    private readonly string _categoryName;

    public RotatingFileLogger(RotatingFileLoggerProvider provider, string categoryName)
    {
        _provider = provider;
        _categoryName = categoryName;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    // El filtrado real por nivel mínimo (sección 17: Information por defecto) lo aplica
    // Microsoft.Extensions.Logging antes de invocar Log (LoggerFilterOptions, configurado en
    // HostConfigurationFactory) — este proveedor no impone un mínimo propio adicional.
    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(formatter);

        var message = formatter(state, exception);

        if (string.IsNullOrEmpty(message) && exception is null)
        {
            return;
        }

        var line = LogSanitizer.Redact(FormatLine(logLevel, _categoryName, message, exception));

        // Warning+ se persiste de inmediato (sección 15/16); Debug/Information queda en el búfer del
        // StreamWriter para no forzar un flush síncrono por cada traza trivial de operación normal.
        _provider.Write(line, flushImmediately: logLevel >= LogLevel.Warning);
    }

    private static string FormatLine(LogLevel logLevel, string categoryName, string message, Exception? exception)
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        var line = $"{timestamp} [{LevelToText(logLevel)}] {categoryName}: {message}";

        return exception is null ? line : $"{line}{Environment.NewLine}{exception}";
    }

    private static string LevelToText(LogLevel logLevel) => logLevel switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        LogLevel.Critical => "CRT",
        _ => "???",
    };
}
