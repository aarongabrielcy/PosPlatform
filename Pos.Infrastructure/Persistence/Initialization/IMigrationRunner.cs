namespace Pos.Infrastructure.Persistence.Initialization;

// Abstracción interna mínima sobre EF Core (GetPendingMigrationsAsync/MigrateAsync) y las
// verificaciones de integridad SQLite, para permitir inyectar fallos en pruebas de integración
// sin tocar migraciones productivas.
internal interface IMigrationRunner
{
    Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken);

    Task MigrateAsync(CancellationToken cancellationToken);

    Task<DatabaseIntegrityStatus> CheckIntegrityAsync(CancellationToken cancellationToken);

    Task CloseConnectionAsync(CancellationToken cancellationToken);
}
