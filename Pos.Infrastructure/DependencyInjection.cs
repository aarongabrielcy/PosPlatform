using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pos.Application.Bootstrap;
using Pos.Application.Branches;
using Pos.Application.Common.Persistence;
using Pos.Application.Common.Time;
using Pos.Application.Inventory;
using Pos.Application.Organizations;
using Pos.Application.Products;
using Pos.Application.RegisterSessions;
using Pos.Application.Registers;
using Pos.Application.Sales;
using Pos.Application.Security;
using Pos.Application.Users;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Persistence.Initialization;
using Pos.Infrastructure.Persistence.Repositories;
using Pos.Infrastructure.Security;
using Pos.Infrastructure.Storage;
using Pos.Infrastructure.Time;

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
        services.AddScoped<IOrganizationRepository, EfOrganizationRepository>();
        services.AddScoped<IBranchRepository, EfBranchRepository>();
        services.AddScoped<IRegisterRepository, EfRegisterRepository>();
        services.AddScoped<IProductRepository, EfProductRepository>();
        services.AddScoped<IRoleRepository, EfRoleRepository>();
        services.AddScoped<IUserRepository, EfUserRepository>();
        services.AddScoped<IRegisterSessionRepository, EfRegisterSessionRepository>();

        services.AddScoped<IInitialBusinessBootstrapService, InitialBusinessBootstrapService>();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
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
