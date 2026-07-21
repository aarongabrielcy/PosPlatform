using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pos.Application.Common.Persistence;
using Pos.Application.Inventory;
using Pos.Application.Sales;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Persistence.Initialization;
using Pos.Infrastructure.Persistence.Repositories;
using Pos.Infrastructure.Storage;

namespace Pos.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPosInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IApplicationPathProvider, ApplicationPathProvider>();

        services.AddDbContext<PosDbContext>((serviceProvider, options) =>
        {
            var pathProvider = serviceProvider.GetRequiredService<IApplicationPathProvider>();
            options.UseSqlite(BuildConnectionString(pathProvider.DatabasePath));
        });

        services.AddScoped<IUnitOfWork>(serviceProvider => serviceProvider.GetRequiredService<PosDbContext>());

        services.AddScoped<IInventoryItemRepository, EfInventoryItemRepository>();
        services.AddScoped<IInventoryMovementRepository, EfInventoryMovementRepository>();
        services.AddScoped<ISaleRepository, EfSaleRepository>();

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ILocalDatabaseInitializer, LocalDatabaseInitializer>();

        return services;
    }

    internal static string BuildConnectionString(string databasePath)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            ForeignKeys = true,
        };

        return builder.ConnectionString;
    }
}
