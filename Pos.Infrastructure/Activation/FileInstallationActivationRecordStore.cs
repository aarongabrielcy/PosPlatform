using System.Text.Json;
using Microsoft.Extensions.Logging;
using Pos.Application.Activation;
using Pos.Infrastructure.Storage;

namespace Pos.Infrastructure.Activation;

// Persiste los metadatos no secretos de activación (InstallationId, ActivatedAtUtc) en un archivo
// JSON plano, separado del secreto (ver DpapiInstallationCredentialStore) y de las tablas de
// negocio de SQLite. No requiere protección: no es información sensible.
public sealed partial class FileInstallationActivationRecordStore : IInstallationActivationRecordStore
{
    private const string ActivationFolderName = "Activation";
    private const string RecordFileName = "activation-state.json";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IApplicationPathProvider _pathProvider;
    private readonly ILogger<FileInstallationActivationRecordStore> _logger;

    public FileInstallationActivationRecordStore(
        IApplicationPathProvider pathProvider,
        ILogger<FileInstallationActivationRecordStore> logger)
    {
        _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> SaveAsync(InstallationActivationRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);

        try
        {
            var directory = GetActivationDirectory();
            Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(record, SerializerOptions);
            await File.WriteAllTextAsync(GetRecordFilePath(), json, cancellationToken).ConfigureAwait(false);

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogSaveFailed(_logger, ex);
            return false;
        }
    }

    public async Task<InstallationActivationRecord?> TryLoadAsync(CancellationToken cancellationToken)
    {
        var filePath = GetRecordFilePath();

        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<InstallationActivationRecord>(json, SerializerOptions);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            LogLoadFailed(_logger, ex);
            return null;
        }
    }

    private string GetActivationDirectory() => Path.Combine(_pathProvider.DataDirectory, ActivationFolderName);

    private string GetRecordFilePath() => Path.Combine(GetActivationDirectory(), RecordFileName);

    [LoggerMessage(Level = LogLevel.Error, Message = "No fue posible guardar los metadatos de activación local.")]
    private static partial void LogSaveFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "No fue posible leer los metadatos de activación local.")]
    private static partial void LogLoadFailed(ILogger logger, Exception exception);
}
