using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Pos.Infrastructure.Persistence;

// Fallback exclusivo para herramientas de diseño (dotnet ef). No se usa en producción.
public sealed class PosDbContextFactory : IDesignTimeDbContextFactory<PosDbContext>
{
    private const string DesignTimeConnectionString = "Data Source=pos-design.db";

    public PosDbContext CreateDbContext(string[] args)
    {
        var connectionString = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
            ? args[0]
            : DesignTimeConnectionString;

        var optionsBuilder = new DbContextOptionsBuilder<PosDbContext>();
        optionsBuilder.UseSqlite(connectionString);

        return new PosDbContext(optionsBuilder.Options);
    }
}
