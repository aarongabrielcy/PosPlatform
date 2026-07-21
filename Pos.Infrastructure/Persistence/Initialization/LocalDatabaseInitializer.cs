using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Pos.Infrastructure.Storage;

namespace Pos.Infrastructure.Persistence.Initialization;

public sealed partial class LocalDatabaseInitializer : ILocalDatabaseInitializer
{
    private readonly IApplicationPathProvider _pathProvider;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<LocalDatabaseInitializer> _logger;
    private readonly IMigrationRunner _migrationRunner;

    public LocalDatabaseInitializer(
        PosDbContext context,
        IApplicationPathProvider pathProvider,
        TimeProvider timeProvider,
        ILogger<LocalDatabaseInitializer> logger)
        : this(pathProvider, timeProvider, logger, new EfMigrationRunner(context))
    {
    }

    // Constructor interno: permite a las pruebas de integración inyectar un IMigrationRunner que
    // simula fallos de migración sin modificar migraciones productivas.
    internal LocalDatabaseInitializer(
        IApplicationPathProvider pathProvider,
        TimeProvider timeProvider,
        ILogger<LocalDatabaseInitializer> logger,
        IMigrationRunner migrationRunner)
    {
        _pathProvider = pathProvider;
        _timeProvider = timeProvider;
        _logger = logger;
        _migrationRunner = migrationRunner;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        LogInitializationStarting(_logger);

        var databaseExisted = File.Exists(_pathProvider.DatabasePath);

        IReadOnlyList<string> pendingMigrations;
        try
        {
            pendingMigrations = await _migrationRunner.GetPendingMigrationsAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogInitializationFailed(_logger, ex);
            await TryCloseConnectionAsync(cancellationToken);
            throw new LocalDatabaseInitializationException(
                "No fue posible leer el estado de la base de datos local.", ex);
        }

        LogPendingMigrationsDetected(_logger, pendingMigrations.Count);

        string? backupPath = null;

        try
        {
            if (pendingMigrations.Count > 0)
            {
                if (databaseExisted)
                {
                    backupPath = CreateBackup();
                    LogBackupCreated(_logger, Path.GetFileName(backupPath));
                }

                await _migrationRunner.MigrateAsync(cancellationToken);
                LogMigrationApplied(_logger, pendingMigrations.Count);
            }

            var integrity = await _migrationRunner.CheckIntegrityAsync(cancellationToken);
            if (!integrity.IsHealthy)
            {
                throw new InvalidOperationException($"La verificación de integridad falló: {integrity.Detail}");
            }

            LogIntegrityVerified(_logger);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogInitializationFailed(_logger, ex);

            try
            {
                await RecoverAsync(databaseExisted, backupPath, cancellationToken);
            }
            catch (Exception recoveryEx)
            {
                LogRecoveryFailed(_logger, recoveryEx);
                throw new LocalDatabaseInitializationException(
                    "La inicialización de la base de datos falló y la recuperación del respaldo también falló. El respaldo se conservó para diagnóstico.",
                    new AggregateException(ex, recoveryEx));
            }

            throw new LocalDatabaseInitializationException(BuildFailureMessage(databaseExisted, backupPath), ex);
        }
    }

    private async Task RecoverAsync(bool databaseExisted, string? backupPath, CancellationToken cancellationToken)
    {
        LogRecoveryStarting(_logger, databaseExisted);

        await _migrationRunner.CloseConnectionAsync(cancellationToken);

        // Microsoft.Data.Sqlite agrupa conexiones en un pool nativo por cadena de conexión: cerrar
        // la conexión de EF no basta para soltar el handle de archivo en Windows. ClearPool fuerza
        // el cierre real antes de eliminar/copiar el archivo.
        ReleaseSqliteConnectionPool();

        if (!databaseExisted)
        {
            DeleteFileIfExists(_pathProvider.DatabasePath);
            DeleteSqliteAuxiliaryFiles();
            LogRecoveryCompleted(_logger);
            return;
        }

        if (backupPath is null)
        {
            // No había respaldo posible (no existían migraciones pendientes antes del fallo de
            // integridad): no se toca el archivo original del usuario.
            return;
        }

        DeleteFileIfExists(_pathProvider.DatabasePath);
        DeleteSqliteAuxiliaryFiles();

        File.Copy(backupPath, _pathProvider.DatabasePath);

        if (!File.Exists(_pathProvider.DatabasePath))
        {
            throw new IOException("El archivo restaurado no está presente después de copiar el respaldo.");
        }

        LogRecoveryCompleted(_logger);
    }

    private string CreateBackup()
    {
        _pathProvider.EnsureBackupDirectoryExists();

        var backupPath = BuildUniqueBackupPath();

        using var source = new SqliteConnection(DependencyInjection.BuildConnectionString(_pathProvider.DatabasePath));
        using var destination = new SqliteConnection(DependencyInjection.BuildConnectionString(backupPath));

        source.Open();
        destination.Open();

        source.BackupDatabase(destination);

        return backupPath;
    }

    private string BuildUniqueBackupPath()
    {
        var timestamp = _timeProvider.GetUtcNow();
        var baseName = $"pos-before-migration-{timestamp:yyyyMMdd-HHmmssfff}";
        var candidate = Path.Combine(_pathProvider.BackupDirectory, baseName + ".db");

        var suffix = 1;
        while (File.Exists(candidate))
        {
            candidate = Path.Combine(_pathProvider.BackupDirectory, $"{baseName}-{suffix}.db");
            suffix++;
        }

        return candidate;
    }

    private async Task TryCloseConnectionAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _migrationRunner.CloseConnectionAsync(cancellationToken);
            ReleaseSqliteConnectionPool();
        }
        catch
        {
            // Limpieza best-effort: no debe ocultar la excepción original ya capturada arriba.
        }
    }

    private void ReleaseSqliteConnectionPool()
    {
        using var connection = new SqliteConnection(DependencyInjection.BuildConnectionString(_pathProvider.DatabasePath));
        SqliteConnection.ClearPool(connection);
    }

    private void DeleteSqliteAuxiliaryFiles()
    {
        DeleteFileIfExists(_pathProvider.DatabasePath + "-wal");
        DeleteFileIfExists(_pathProvider.DatabasePath + "-shm");
    }

    private static void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static string BuildFailureMessage(bool databaseExisted, string? backupPath)
    {
        if (!databaseExisted)
        {
            return "No fue posible crear la base de datos local en el primer inicio. Se eliminaron los archivos parciales.";
        }

        return backupPath is not null
            ? "No fue posible aplicar las migraciones pendientes. Se restauró el respaldo previo a la migración."
            : "No fue posible validar la integridad de la base de datos local existente.";
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Iniciando validación de la base de datos local.")]
    private static partial void LogInitializationStarting(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Migraciones pendientes detectadas: {PendingCount}.")]
    private static partial void LogPendingMigrationsDetected(ILogger logger, int pendingCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Respaldo creado antes de migrar: {BackupFileName}.")]
    private static partial void LogBackupCreated(ILogger logger, string backupFileName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Migraciones aplicadas correctamente ({AppliedCount}).")]
    private static partial void LogMigrationApplied(ILogger logger, int appliedCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Verificación de integridad de la base de datos local exitosa.")]
    private static partial void LogIntegrityVerified(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Fallo durante la inicialización de la base de datos local.")]
    private static partial void LogInitializationFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Iniciando recuperación tras fallo de inicialización (existía base previa: {DatabaseExisted}).")]
    private static partial void LogRecoveryStarting(ILogger logger, bool databaseExisted);

    [LoggerMessage(Level = LogLevel.Information, Message = "Recuperación tras fallo de inicialización completada.")]
    private static partial void LogRecoveryCompleted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Fallo también durante la recuperación tras un error de inicialización.")]
    private static partial void LogRecoveryFailed(ILogger logger, Exception exception);
}
