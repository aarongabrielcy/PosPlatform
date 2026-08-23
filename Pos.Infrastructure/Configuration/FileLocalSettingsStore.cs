using System.Text.Json;
using Microsoft.Extensions.Logging;
using Pos.Application.Configuration;
using Pos.Infrastructure.Storage;

namespace Pos.Infrastructure.Configuration;

// BASIC-CFG-01, sección 6/8/9/10: persiste la configuración local de máquina (impresora/cajón) en
// %LOCALAPPDATA%\PosPlatform\Data\Config\settings.json, en una carpeta propia dentro del mismo
// DataDirectory que ya usan Activation/Enforcement (ver ApplicationPathProvider) - nunca dentro del
// directorio de instalación (sección 6: un futuro instalador podría colocar la app bajo Program
// Files, no escribible por un usuario normal). Escritura atómica (archivo temporal + File.Replace/
// Move) mismo patrón que FileInstallationEnforcementStateStore (sección 9 de la tarea: "a crash
// mid-write must never leave a half-written configuration"). No es secreto: nombre de impresora y
// preferencias de cajón no requieren DPAPI.
//
// LoadAsync devuelve null tanto si no existe archivo (primer arranque) como si existe pero está
// corrupto/ilegible (sección 10: "corrupt local config -> safe defaults -> application still
// starts"): en ambos casos el llamador (IReceiptPrinterOptionsProvider en Pos.Desktop) recurre a los
// valores por defecto vigentes de appsettings/appsettings.Local.json, nunca reinterpreta un valor
// parcial o inválido.
public sealed partial class FileLocalSettingsStore : ILocalSettingsStore
{
    private const string ConfigFolderName = "Config";
    private const string SettingsFileName = "settings.json";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IApplicationPathProvider _pathProvider;
    private readonly ILogger<FileLocalSettingsStore> _logger;

    public FileLocalSettingsStore(IApplicationPathProvider pathProvider, ILogger<FileLocalSettingsStore> logger)
    {
        _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<LocalSettings?> LoadAsync(CancellationToken cancellationToken = default)
    {
        var filePath = GetSettingsFilePath();

        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
            var settings = JsonSerializer.Deserialize<LocalSettings>(json, SerializerOptions)
                ?? throw new JsonException("El archivo de configuración local se deserializó como null.");

            return settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            LogCorruptSettings(_logger, ex);
            return null;
        }
    }

    public async Task<bool> SaveAsync(LocalSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            var directory = GetConfigDirectory();
            Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(settings, SerializerOptions);
            await WriteAtomicAsync(GetSettingsFilePath(), json, cancellationToken).ConfigureAwait(false);

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogSaveFailed(_logger, ex);
            return false;
        }
    }

    // Mismo patrón que FileInstallationEnforcementStateStore.WriteAtomicAsync: un crash entre ambos
    // pasos deja el archivo temporal huérfano, nunca el archivo final truncado.
    private static async Task WriteAtomicAsync(string filePath, string content, CancellationToken cancellationToken)
    {
        var tempPath = filePath + ".tmp";

        await File.WriteAllTextAsync(tempPath, content, cancellationToken).ConfigureAwait(false);

        if (File.Exists(filePath))
        {
            File.Replace(tempPath, filePath, null);
        }
        else
        {
            File.Move(tempPath, filePath);
        }
    }

    private string GetConfigDirectory() => Path.Combine(_pathProvider.DataDirectory, ConfigFolderName);

    private string GetSettingsFilePath() => Path.Combine(GetConfigDirectory(), SettingsFileName);

    [LoggerMessage(Level = LogLevel.Error, Message = "No fue posible guardar la configuración local de máquina.")]
    private static partial void LogSaveFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "La configuración local de máquina es ilegible o está corrupta; se usarán los valores por defecto.")]
    private static partial void LogCorruptSettings(ILogger logger, Exception exception);
}
