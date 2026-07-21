using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace Pos.Infrastructure.Persistence.Initialization;

internal sealed class EfMigrationRunner : IMigrationRunner
{
    private readonly PosDbContext _context;

    public EfMigrationRunner(PosDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken)
    {
        var pending = await _context.Database.GetPendingMigrationsAsync(cancellationToken);
        return pending.ToList();
    }

    public Task MigrateAsync(CancellationToken cancellationToken) =>
        _context.Database.MigrateAsync(cancellationToken);

    public async Task<DatabaseIntegrityStatus> CheckIntegrityAsync(CancellationToken cancellationToken)
    {
        var connection = _context.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;

        if (!wasOpen)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            var integrityResult = await ExecuteScalarStringAsync(connection, "PRAGMA integrity_check;", cancellationToken);
            if (!string.Equals(integrityResult, "ok", StringComparison.Ordinal))
            {
                return new DatabaseIntegrityStatus(false, $"integrity_check devolvió: {integrityResult}");
            }

            var foreignKeyViolationCount = await CountRowsAsync(connection, "PRAGMA foreign_key_check;", cancellationToken);
            if (foreignKeyViolationCount > 0)
            {
                return new DatabaseIntegrityStatus(false, $"foreign_key_check devolvió {foreignKeyViolationCount} fila(s)");
            }

            return new DatabaseIntegrityStatus(true, null);
        }
        finally
        {
            if (!wasOpen)
            {
                await connection.CloseAsync();
            }
        }
    }

    public Task CloseConnectionAsync(CancellationToken cancellationToken) =>
        _context.Database.CloseConnectionAsync();

    private static async Task<string?> ExecuteScalarStringAsync(DbConnection connection, string commandText, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result as string;
    }

    private static async Task<int> CountRowsAsync(DbConnection connection, string commandText, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;

        var count = 0;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            count++;
        }

        return count;
    }
}
