using System.Text.Json;
using Microsoft.Extensions.Logging;
using Pos.Application.Enforcement;
using Pos.Infrastructure.Storage;

namespace Pos.Infrastructure.Enforcement;

// Persiste el estado de enforcement restrictivo (Suspended/CredentialInvalid/Decommissioned) en un
// archivo JSON plano, en una carpeta propia separada de Activation (ver sección 13 de la tarea:
// "no en tablas Sale/Product", "un almacén local pequeño y dedicado"). No es secreto: a diferencia
// de la Installation Credential, no requiere DPAPI.
//
// Allowed nunca se escribe a disco (ver ClearAsync): su ausencia de archivo ES el valor Allowed
// (mismo patrón que FileInstallationActivationRecordStore con "sin registro = NotActivated").
//
// A diferencia de FileInstallationActivationRecordStore (sobrescritura directa), este store escribe
// con reemplazo atómico (archivo temporal + File.Replace/Move) porque la sección 14 de la tarea
// exige explícitamente que un crash a mitad de escritura nunca deje un archivo truncado/corrupto.
public sealed partial class FileInstallationEnforcementStateStore : IInstallationEnforcementStateStore
{
    private const string EnforcementFolderName = "Enforcement";
    private const string StateFileName = "enforcement-state.json";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IApplicationPathProvider _pathProvider;
    private readonly ILogger<FileInstallationEnforcementStateStore> _logger;

    public FileInstallationEnforcementStateStore(
        IApplicationPathProvider pathProvider,
        ILogger<FileInstallationEnforcementStateStore> logger)
    {
        _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<InstallationEnforcementState?> TryLoadAsync(CancellationToken cancellationToken)
    {
        var filePath = GetStateFilePath();

        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
            var record = JsonSerializer.Deserialize<PersistedState>(json, SerializerOptions)
                ?? throw new JsonException("El archivo de estado de enforcement se deserializó como null.");

            return record.State;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Fail-safe de la sección 14: un archivo presente pero ilegible/corrupto nunca se
            // interpreta como Allowed. Suspended es el valor determinístico de respaldo (ver
            // IInstallationEnforcementStateStore).
            LogCorruptState(_logger, ex);
            return InstallationEnforcementState.Suspended;
        }
    }

    public async Task<bool> SaveAsync(InstallationEnforcementState state, CancellationToken cancellationToken)
    {
        if (state == InstallationEnforcementState.Allowed)
        {
            throw new ArgumentException(
                "Allowed no se persiste explícitamente; use ClearAsync.", nameof(state));
        }

        try
        {
            var directory = GetEnforcementDirectory();
            Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(new PersistedState(state), SerializerOptions);
            await WriteAtomicAsync(GetStateFilePath(), json, cancellationToken).ConfigureAwait(false);

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogSaveFailed(_logger, ex);
            return false;
        }
    }

    public Task<bool> ClearAsync(CancellationToken cancellationToken)
    {
        try
        {
            var filePath = GetStateFilePath();

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            return Task.FromResult(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogClearFailed(_logger, ex);
            return Task.FromResult(false);
        }
    }

    // Escribe en un archivo temporal y lo promueve con File.Replace (o File.Move si el destino
    // todavía no existe): un crash entre ambos pasos deja el archivo temporal huérfano, nunca el
    // archivo final truncado (sección 14).
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

    private string GetEnforcementDirectory() => Path.Combine(_pathProvider.DataDirectory, EnforcementFolderName);

    private string GetStateFilePath() => Path.Combine(GetEnforcementDirectory(), StateFileName);

    private sealed record PersistedState(InstallationEnforcementState State);

    [LoggerMessage(Level = LogLevel.Error, Message = "No fue posible guardar el estado de enforcement local.")]
    private static partial void LogSaveFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "El estado de enforcement local es ilegible o está corrupto; se asume Suspended como valor de respaldo seguro.")]
    private static partial void LogCorruptState(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "No fue posible limpiar el estado de enforcement local.")]
    private static partial void LogClearFailed(ILogger logger, Exception exception);
}
