using System.Text;
using Microsoft.Extensions.Logging;

namespace Pos.Infrastructure.Logging;

// BASIC-REL-01, sección 12-15: proveedor de registros persistentes con rotación determinística por
// tamaño, sin agregar Serilog/NLog (sección 14 de la tarea: "preferir una implementación pequeña
// compatible con Microsoft.Extensions.Logging" — el proyecto no tenía ningún proveedor de archivo
// existente que reutilizar, verificado por auditoría).
//
// Política de rotación (sección 13): archivo activo "posplatform.log" + hasta 9 archivos rotados
// "posplatform.1.log".."posplatform.9.log" (10 archivos retenidos en total), cada uno acotado a
// MaxFileSizeBytes (5 MiB por defecto) — retención máxima aproximada de 50 MiB.
//
// El directorio de registros se crea de forma perezosa en la primera escritura real, nunca en el
// constructor (sección 48: las pruebas que solo construyen el host sin loguear nada no deben crear
// carpetas en el LocalAppData real de la máquina que ejecuta las pruebas).
public sealed class RotatingFileLoggerProvider : ILoggerProvider
{
    public const long DefaultMaxFileSizeBytes = 5 * 1024 * 1024;
    public const int DefaultMaxRotatedFiles = 9;

    private const string ActiveFileName = "posplatform.log";

    private readonly string _logDirectory;
    private readonly long _maxFileSizeBytes;
    private readonly int _maxRotatedFiles;
    private readonly object _writeLock = new();

    private StreamWriter? _writer;
    private long _currentFileSize;
    private bool _directoryEnsured;
    private bool _disposed;

    public RotatingFileLoggerProvider(
        string logDirectory,
        long maxFileSizeBytes = DefaultMaxFileSizeBytes,
        int maxRotatedFiles = DefaultMaxRotatedFiles)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logDirectory);

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxFileSizeBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxRotatedFiles);

        _logDirectory = logDirectory;
        _maxFileSizeBytes = maxFileSizeBytes;
        _maxRotatedFiles = maxRotatedFiles;
    }

    public string LogDirectory => _logDirectory;

    public string ActiveFilePath => Path.Combine(_logDirectory, ActiveFileName);

    public ILogger CreateLogger(string categoryName) => new RotatingFileLogger(this, categoryName);

    // Warning+ (flushImmediately=true) se persiste en disco de inmediato (sección 15: "eventos de
    // arranque/fatales importantes deben persistirse de forma confiable"). Debug/Information solo se
    // almacenan en el búfer del StreamWriter y se vuelcan en el siguiente flush inmediato, rotación o
    // Dispose — evita un flush síncrono por cada traza trivial (sección 15).
    //
    // Best-effort deliberado (sección 15/48): un fallo de E/S al escribir el registro nunca debe
    // propagarse al código que originó el log (arriesgaría degradar la operación normal del POS por
    // un problema puramente de diagnóstico).
    internal void Write(string line, bool flushImmediately)
    {
        lock (_writeLock)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                EnsureDirectoryLocked();

                var lineWithNewLine = line + Environment.NewLine;
                var lineBytes = Encoding.UTF8.GetByteCount(lineWithNewLine);

                if (_writer is null)
                {
                    OpenWriterLocked();
                }

                if (_currentFileSize > 0 && _currentFileSize + lineBytes > _maxFileSizeBytes)
                {
                    RotateLocked();
                }

                _writer!.Write(lineWithNewLine);
                _currentFileSize += lineBytes;

                if (flushImmediately)
                {
                    _writer.Flush();
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private void EnsureDirectoryLocked()
    {
        if (_directoryEnsured)
        {
            return;
        }

        Directory.CreateDirectory(_logDirectory);
        _directoryEnsured = true;
    }

    private void OpenWriterLocked()
    {
        // FileShare.ReadWrite (no solo Read): en Windows, un lector externo abierto con las banderas
        // de compartición por defecto (p. ej. File.ReadAllText, Notepad, "Get-Content -Tail") declara
        // su propio FileShare.Read, que por sí solo NO basta para coexistir con un escritor —
        // CreateFile exige que el share del lector también permita el acceso de Write que ya tiene
        // el escritor. Ampliar aquí a ReadWrite es lo que permite inspeccionar el registro activo
        // (sección 22: "Abrir carpeta de registros") sin cerrar la aplicación primero.
        var stream = new FileStream(ActiveFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = false };

        // FileMode.Append posiciona al final, pero Length ya refleja el contenido existente si el
        // proceso se reinicia con un archivo activo previo (sección 48: "restart/reopen behavior") —
        // la rotación sigue disparándose correctamente en el punto correcto tras un reinicio.
        _currentFileSize = stream.Length;
    }

    private void RotateLocked()
    {
        _writer?.Flush();
        _writer?.Dispose();
        _writer = null;

        var oldestPath = GetRotatedPath(_maxRotatedFiles);
        if (File.Exists(oldestPath))
        {
            File.Delete(oldestPath);
        }

        for (var i = _maxRotatedFiles - 1; i >= 1; i--)
        {
            var source = GetRotatedPath(i);
            if (File.Exists(source))
            {
                File.Move(source, GetRotatedPath(i + 1));
            }
        }

        if (File.Exists(ActiveFilePath))
        {
            File.Move(ActiveFilePath, GetRotatedPath(1));
        }

        OpenWriterLocked();
    }

    private string GetRotatedPath(int index) => Path.Combine(_logDirectory, $"posplatform.{index}.log");

    public void Dispose()
    {
        lock (_writeLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _writer?.Flush();
            _writer?.Dispose();
            _writer = null;
        }
    }
}
