using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Persistence.Initialization;
using Pos.Infrastructure.Persistence.Records;
using Pos.Infrastructure.Storage;

namespace Pos.Infrastructure.Tests.Persistence.Initialization;

// Pruebas de integración con SQLite real sobre directorios temporales únicos por prueba. No debe
// tocarse jamás la base real del usuario en %LocalAppData%\PosPlatform\Data\pos.db.
public sealed class LocalDatabaseInitializerTests : IDisposable
{
    private readonly string _root;
    private readonly ApplicationPathProvider _pathProvider;

    public LocalDatabaseInitializerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "PosPlatformInitializerTests_" + Guid.NewGuid());
        _pathProvider = new ApplicationPathProvider(_root);

        // Refleja App.OnStartup: el composition root crea el DataDirectory explícitamente antes de
        // resolver el inicializador; SQLite no crea directorios intermedios por sí mismo.
        _pathProvider.EnsureDataDirectoryExists();
    }

    public void Dispose()
    {
        // Microsoft.Data.Sqlite poolea conexiones nativas por cadena de conexión: cerrar una
        // conexión no libera el handle de archivo en Windows hasta limpiar su pool. Se limpia el
        // pool de pos.db y el de cada respaldo generado antes de borrar el directorio temporal.
        ClearPoolFor(_pathProvider.DatabasePath);

        if (Directory.Exists(_pathProvider.BackupDirectory))
        {
            foreach (var backupFile in Directory.GetFiles(_pathProvider.BackupDirectory))
            {
                ClearPoolFor(backupFile);
            }
        }

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task FirstExecutionAppliesInitialCreateWithoutBackup()
    {
        await using var context = CreateContext();

        await CreateInitializer(context).InitializeAsync();

        Assert.True(File.Exists(_pathProvider.DatabasePath));
        Assert.False(Directory.Exists(_pathProvider.BackupDirectory));

        var tableNames = await GetTableNamesAsync(_pathProvider.DatabasePath);
        var domainTables = tableNames
            .Where(name => !name.StartsWith("sqlite_", StringComparison.Ordinal) && name != "__EFMigrationsHistory")
            .ToList();

        var expectedDomainTables = new[]
        {
            "branches",
            "inventory_items",
            "inventory_movements",
            "organizations",
            "payments",
            "product_audit_changes",
            "product_audit_events",
            "products",
            "register_sessions",
            "registers",
            "role_permissions",
            "roles",
            "sale_lines",
            "sales",
            "users",
        };

        Assert.Equal(
            expectedDomainTables.OrderBy(name => name, StringComparer.Ordinal),
            domainTables.OrderBy(name => name, StringComparer.Ordinal));
        Assert.Contains("__EFMigrationsHistory", tableNames);

        Assert.Equal("ok", await RunIntegrityCheckAsync(_pathProvider.DatabasePath));
        Assert.Equal(0, await CountForeignKeyViolationsAsync(_pathProvider.DatabasePath));
    }

    [Fact]
    public async Task RunningInitializeAgainOnAnUpToDateDatabaseDoesNotBackupOrAlterData()
    {
        await using (var context = CreateContext())
        {
            await CreateInitializer(context).InitializeAsync();
        }

        var organizationId = Guid.NewGuid();
        await using (var seedContext = CreateContext())
        {
            seedContext.Add(new OrganizationRecord
            {
                Id = organizationId,
                Name = "Acme",
                IsActive = true,
                CreatedAtUtc = DateTimeOffset.UtcNow,
            });
            await seedContext.CommitAsync(CancellationToken.None);
        }

        await using (var context = CreateContext())
        {
            await CreateInitializer(context).InitializeAsync();
        }

        Assert.False(Directory.Exists(_pathProvider.BackupDirectory));

        await using var verifyContext = CreateContext();
        var organization = await verifyContext.Set<OrganizationRecord>().SingleAsync(o => o.Id == organizationId);
        Assert.Equal("Acme", organization.Name);
    }

    [Fact]
    public async Task PreviousValidDatabaseWithPendingMigrationIsBackedUpBeforeMigrating()
    {
        SeedLegacyMarkerTable(_pathProvider.DatabasePath, "pre-migration-data");

        await using var context = CreateContext();
        await CreateInitializer(context).InitializeAsync();

        Assert.True(Directory.Exists(_pathProvider.BackupDirectory));
        var backupPath = Assert.Single(Directory.GetFiles(_pathProvider.BackupDirectory));
        Assert.Matches(@"^pos-before-migration-\d{8}-\d{9}\.db$", Path.GetFileName(backupPath));

        Assert.Equal("pre-migration-data", ReadLegacyMarkerNote(backupPath));

        var tableNames = await GetTableNamesAsync(_pathProvider.DatabasePath);
        Assert.Contains("organizations", tableNames);
        Assert.Contains("__EFMigrationsHistory", tableNames);
    }

    [Fact]
    public async Task MigrationFailureWithPreviousDatabaseRestoresBackupAndKeepsIt()
    {
        SeedLegacyMarkerTable(_pathProvider.DatabasePath, "must-survive-rollback");

        await using var context = CreateContext();
        var faultyRunner = new FaultyMigrationRunner(new EfMigrationRunner(context));
        var initializer = CreateInitializer(faultyRunner);

        var exception = await Assert.ThrowsAsync<LocalDatabaseInitializationException>(
            () => initializer.InitializeAsync());

        Assert.IsType<InvalidOperationException>(exception.InnerException);

        Assert.False(File.Exists(_pathProvider.DatabasePath + "-wal"));
        Assert.False(File.Exists(_pathProvider.DatabasePath + "-shm"));

        var backupPath = Assert.Single(Directory.GetFiles(_pathProvider.BackupDirectory));
        Assert.Equal("must-survive-rollback", ReadLegacyMarkerNote(_pathProvider.DatabasePath));
        Assert.True(File.Exists(backupPath));

        var tableNames = await GetTableNamesAsync(_pathProvider.DatabasePath);
        Assert.DoesNotContain("organizations", tableNames);
    }

    [Fact]
    public async Task MigrationFailureOnFirstExecutionLeavesNoDatabaseFiles()
    {
        await using var context = CreateContext();
        var faultyRunner = new FaultyMigrationRunner(new EfMigrationRunner(context));
        var initializer = CreateInitializer(faultyRunner);

        var exception = await Assert.ThrowsAsync<LocalDatabaseInitializationException>(
            () => initializer.InitializeAsync());

        Assert.IsType<InvalidOperationException>(exception.InnerException);

        Assert.False(File.Exists(_pathProvider.DatabasePath));
        Assert.False(File.Exists(_pathProvider.DatabasePath + "-wal"));
        Assert.False(File.Exists(_pathProvider.DatabasePath + "-shm"));
        Assert.False(Directory.Exists(_pathProvider.BackupDirectory));
    }

    [Fact]
    public async Task CorruptDatabaseFileFailsWithoutSilentlyReplacingTheOriginal()
    {
        var corruptBytes = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 };
        await File.WriteAllBytesAsync(_pathProvider.DatabasePath, corruptBytes);

        await using var context = CreateContext();
        var initializer = CreateInitializer(context);

        var exception = await Assert.ThrowsAsync<LocalDatabaseInitializationException>(
            () => initializer.InitializeAsync());

        Assert.DoesNotContain("restaur", exception.Message, StringComparison.OrdinalIgnoreCase);

        Assert.True(File.Exists(_pathProvider.DatabasePath));
        Assert.Equal(corruptBytes, await File.ReadAllBytesAsync(_pathProvider.DatabasePath));
        Assert.False(Directory.Exists(_pathProvider.BackupDirectory));
    }

    private PosDbContext CreateContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<PosDbContext>();
        optionsBuilder.UseSqlite(DependencyInjection.BuildConnectionString(_pathProvider.DatabasePath));
        return new PosDbContext(optionsBuilder.Options);
    }

    private LocalDatabaseInitializer CreateInitializer(PosDbContext context) =>
        new(context, _pathProvider, TimeProvider.System, new NoOpLogger<LocalDatabaseInitializer>());

    private LocalDatabaseInitializer CreateInitializer(IMigrationRunner migrationRunner) =>
        new(_pathProvider, TimeProvider.System, new NoOpLogger<LocalDatabaseInitializer>(), migrationRunner);

    private static void ClearPoolFor(string databasePath)
    {
        using var connection = new SqliteConnection(DependencyInjection.BuildConnectionString(databasePath));
        SqliteConnection.ClearPool(connection);
    }

    private static void SeedLegacyMarkerTable(string databasePath, string note)
    {
        using (var connection = new SqliteConnection(DependencyInjection.BuildConnectionString(databasePath)))
        {
            connection.Open();

            using var createCommand = connection.CreateCommand();
            createCommand.CommandText = "CREATE TABLE legacy_marker (id INTEGER PRIMARY KEY, note TEXT NOT NULL);";
            createCommand.ExecuteNonQuery();

            using var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = "INSERT INTO legacy_marker (id, note) VALUES (1, @note);";
            insertCommand.Parameters.AddWithValue("@note", note);
            insertCommand.ExecuteNonQuery();
        }

        ClearPoolFor(databasePath);
    }

    private static string? ReadLegacyMarkerNote(string databasePath)
    {
        string? note;

        using (var connection = new SqliteConnection(DependencyInjection.BuildConnectionString(databasePath)))
        {
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT note FROM legacy_marker WHERE id = 1;";
            note = (string?)command.ExecuteScalar();
        }

        ClearPoolFor(databasePath);
        return note;
    }

    private static async Task<List<string>> GetTableNamesAsync(string databasePath)
    {
        var names = new List<string>();

        await using (var connection = new SqliteConnection(DependencyInjection.BuildConnectionString(databasePath)))
        {
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                names.Add(reader.GetString(0));
            }
        }

        ClearPoolFor(databasePath);
        return names;
    }

    private static async Task<string?> RunIntegrityCheckAsync(string databasePath)
    {
        string? result;

        await using (var connection = new SqliteConnection(DependencyInjection.BuildConnectionString(databasePath)))
        {
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check;";
            result = (string?)await command.ExecuteScalarAsync();
        }

        ClearPoolFor(databasePath);
        return result;
    }

    private static async Task<int> CountForeignKeyViolationsAsync(string databasePath)
    {
        var count = 0;

        await using (var connection = new SqliteConnection(DependencyInjection.BuildConnectionString(databasePath)))
        {
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA foreign_key_check;";

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                count++;
            }
        }

        ClearPoolFor(databasePath);
        return count;
    }

    // Delega en un EfMigrationRunner real para GetPendingMigrationsAsync/CheckIntegrityAsync, pero
    // simula un fallo de migración *después* de que la migración real ya modificó el archivo, para
    // ejercitar la restauración del respaldo sin tocar migraciones productivas.
    private sealed class FaultyMigrationRunner : IMigrationRunner
    {
        private readonly IMigrationRunner _inner;

        public FaultyMigrationRunner(IMigrationRunner inner)
        {
            _inner = inner;
        }

        public Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken) =>
            _inner.GetPendingMigrationsAsync(cancellationToken);

        public async Task MigrateAsync(CancellationToken cancellationToken)
        {
            await _inner.MigrateAsync(cancellationToken);
            throw new InvalidOperationException("Fallo simulado de migración para pruebas de recuperación.");
        }

        public Task<DatabaseIntegrityStatus> CheckIntegrityAsync(CancellationToken cancellationToken) =>
            _inner.CheckIntegrityAsync(cancellationToken);

        public Task CloseConnectionAsync(CancellationToken cancellationToken) =>
            _inner.CloseConnectionAsync(cancellationToken);
    }

    private sealed class NoOpLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => false;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }
    }
}
