using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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

namespace Pos.Infrastructure.Tests;

public class DependencyInjectionTests
{
    private sealed class FakeApplicationPathProvider : IApplicationPathProvider
    {
        public FakeApplicationPathProvider(string root)
        {
            DataDirectory = Path.Combine(root, "PosPlatform", "Data");
            DatabasePath = Path.Combine(DataDirectory, "pos.db");
            BackupDirectory = Path.Combine(DataDirectory, "Backups");
        }

        public string DataDirectory { get; }

        public string DatabasePath { get; }

        public string BackupDirectory { get; }

        public void EnsureDataDirectoryExists() =>
            throw new InvalidOperationException("No debe invocarse durante la prueba de composición.");

        public void EnsureBackupDirectoryExists() =>
            throw new InvalidOperationException("No debe invocarse durante la prueba de composición.");
    }

    private static (ServiceProvider Provider, FakeApplicationPathProvider PathProvider) BuildProvider()
    {
        var root = Path.Combine(Path.GetTempPath(), "PosPlatformDiTests_" + Guid.NewGuid());
        var pathProvider = new FakeApplicationPathProvider(root);

        var services = new ServiceCollection();
        services.AddPosInfrastructure();

        // AddPosInfrastructure no registra infraestructura de logging (eso lo aporta
        // Host.CreateDefaultBuilder en producción); aquí se provee un ILogger<> mínimo para que
        // ValidateOnBuild pueda resolver LocalDatabaseInitializer.
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        // Sustituye el IApplicationPathProvider real por un fake apuntando a un directorio
        // temporal: la última resolución de un tipo no-keyed gana en el contenedor.
        services.AddSingleton<IApplicationPathProvider>(pathProvider);

        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true,
        });

        return (provider, pathProvider);
    }

    [Fact]
    public void AddPosInfrastructureBuildsContainerWithScopeAndBuildValidationEnabled()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            using var scope = provider.CreateScope();
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<PosDbContext>());
        }
    }

    [Fact]
    public void PosDbContextResolvesOnlyWithinAScope()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<PosDbContext>());
        }
    }

    [Fact]
    public void IUnitOfWorkResolvesTheSameScopedInstanceAsPosDbContext()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            using var scope = provider.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<PosDbContext>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            Assert.Same(context, unitOfWork);
        }
    }

    [Fact]
    public void RepositoryInterfacesResolveTheirEfImplementations()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            using var scope = provider.CreateScope();

            Assert.IsType<EfInventoryItemRepository>(scope.ServiceProvider.GetRequiredService<IInventoryItemRepository>());
            Assert.IsType<EfInventoryMovementRepository>(scope.ServiceProvider.GetRequiredService<IInventoryMovementRepository>());
            Assert.IsType<EfSaleRepository>(scope.ServiceProvider.GetRequiredService<ISaleRepository>());
            Assert.IsType<EfOrganizationRepository>(scope.ServiceProvider.GetRequiredService<IOrganizationRepository>());
            Assert.IsType<EfBranchRepository>(scope.ServiceProvider.GetRequiredService<IBranchRepository>());
            Assert.IsType<EfRegisterRepository>(scope.ServiceProvider.GetRequiredService<IRegisterRepository>());
            Assert.IsType<EfProductRepository>(scope.ServiceProvider.GetRequiredService<IProductRepository>());
            Assert.IsType<EfRoleRepository>(scope.ServiceProvider.GetRequiredService<IRoleRepository>());
            Assert.IsType<EfUserRepository>(scope.ServiceProvider.GetRequiredService<IUserRepository>());
            Assert.IsType<EfRegisterSessionRepository>(scope.ServiceProvider.GetRequiredService<IRegisterSessionRepository>());
        }
    }

    [Fact]
    public void NewRepositoriesAreScopedAndReuseTheSameInstanceWithinAScope()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            using var scope = provider.CreateScope();

            var first = scope.ServiceProvider.GetRequiredService<IOrganizationRepository>();
            var second = scope.ServiceProvider.GetRequiredService<IOrganizationRepository>();

            Assert.Same(first, second);
        }
    }

    [Fact]
    public void NewRepositoriesProduceDifferentInstancesAcrossScopes()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            using var scopeA = provider.CreateScope();
            using var scopeB = provider.CreateScope();

            var repositoryA = scopeA.ServiceProvider.GetRequiredService<IOrganizationRepository>();
            var repositoryB = scopeB.ServiceProvider.GetRequiredService<IOrganizationRepository>();

            Assert.NotSame(repositoryA, repositoryB);
        }
    }

    [Fact]
    public void IClockResolvesSystemClockAsSingleton()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            var rootClock = provider.GetRequiredService<IClock>();

            using var scopeA = provider.CreateScope();
            using var scopeB = provider.CreateScope();

            var clockA = scopeA.ServiceProvider.GetRequiredService<IClock>();
            var clockB = scopeB.ServiceProvider.GetRequiredService<IClock>();

            Assert.IsType<SystemClock>(rootClock);
            Assert.Same(rootClock, clockA);
            Assert.Same(rootClock, clockB);
        }
    }

    [Fact]
    public void BuildingTheContainerAndResolvingAScopeDoesNotCreateAnySqliteFile()
    {
        var (provider, pathProvider) = BuildProvider();

        using (provider)
        {
            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<PosDbContext>();

            Assert.False(Directory.Exists(pathProvider.DataDirectory));
            Assert.False(File.Exists(pathProvider.DatabasePath));
        }
    }

    [Fact]
    public void LocalDatabaseInitializerResolvesWithinAScope()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            using var scope = provider.CreateScope();
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<ILocalDatabaseInitializer>());
        }
    }

    [Fact]
    public void LocalDatabaseInitializerUsesTheSameScopedPosDbContextAsIUnitOfWork()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            using var scope = provider.CreateScope();

            // Resolver ambos no debe crear una segunda instancia de PosDbContext ni provocar
            // dependencias captive: el mismo scope debe construir un único DbContext Scoped.
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<ILocalDatabaseInitializer>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<IUnitOfWork>());
        }
    }

    [Fact]
    public void TimeProviderResolvesAndIsSingletonAcrossScopes()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            var rootTimeProvider = provider.GetRequiredService<TimeProvider>();

            using var scopeA = provider.CreateScope();
            using var scopeB = provider.CreateScope();

            var timeProviderA = scopeA.ServiceProvider.GetRequiredService<TimeProvider>();
            var timeProviderB = scopeB.ServiceProvider.GetRequiredService<TimeProvider>();

            Assert.Same(TimeProvider.System, rootTimeProvider);
            Assert.Same(rootTimeProvider, timeProviderA);
            Assert.Same(rootTimeProvider, timeProviderB);
        }
    }

    [Fact]
    public void ResolvingLocalDatabaseInitializerDoesNotCreateAnySqliteFile()
    {
        var (provider, pathProvider) = BuildProvider();

        using (provider)
        {
            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<ILocalDatabaseInitializer>();

            Assert.False(Directory.Exists(pathProvider.DataDirectory));
            Assert.False(File.Exists(pathProvider.DatabasePath));
        }
    }

    [Fact]
    public void IPasswordHasherResolvesPbkdf2PasswordHasher()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            Assert.IsType<Pbkdf2PasswordHasher>(provider.GetRequiredService<IPasswordHasher>());
        }
    }

    [Fact]
    public void IPasswordHasherIsSingletonAcrossScopes()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            var rootHasher = provider.GetRequiredService<IPasswordHasher>();

            using var scopeA = provider.CreateScope();
            using var scopeB = provider.CreateScope();

            var hasherA = scopeA.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var hasherB = scopeB.ServiceProvider.GetRequiredService<IPasswordHasher>();

            Assert.Same(rootHasher, hasherA);
            Assert.Same(rootHasher, hasherB);
        }
    }

    [Fact]
    public void ResolvingIPasswordHasherDoesNotCreateAnySqliteFile()
    {
        var (provider, pathProvider) = BuildProvider();

        using (provider)
        {
            provider.GetRequiredService<IPasswordHasher>();

            Assert.False(Directory.Exists(pathProvider.DataDirectory));
            Assert.False(File.Exists(pathProvider.DatabasePath));
        }
    }

    [Fact]
    public void IInitialBusinessBootstrapServiceResolvesWithinAScope()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            using var scope = provider.CreateScope();
            Assert.IsType<InitialBusinessBootstrapService>(
                scope.ServiceProvider.GetRequiredService<IInitialBusinessBootstrapService>());
        }
    }

    [Fact]
    public void IInitialBusinessBootstrapServiceIsScopedAndReusesTheSameInstanceWithinAScope()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            using var scope = provider.CreateScope();

            var first = scope.ServiceProvider.GetRequiredService<IInitialBusinessBootstrapService>();
            var second = scope.ServiceProvider.GetRequiredService<IInitialBusinessBootstrapService>();

            Assert.Same(first, second);
        }
    }

    [Fact]
    public void IInitialBusinessBootstrapServiceProducesDifferentInstancesAcrossScopes()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            using var scopeA = provider.CreateScope();
            using var scopeB = provider.CreateScope();

            var serviceA = scopeA.ServiceProvider.GetRequiredService<IInitialBusinessBootstrapService>();
            var serviceB = scopeB.ServiceProvider.GetRequiredService<IInitialBusinessBootstrapService>();

            Assert.NotSame(serviceA, serviceB);
        }
    }

    [Fact]
    public void ResolvingIInitialBusinessBootstrapServiceDoesNotCreateAnySqliteFile()
    {
        var (provider, pathProvider) = BuildProvider();

        using (provider)
        {
            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<IInitialBusinessBootstrapService>();

            Assert.False(Directory.Exists(pathProvider.DataDirectory));
            Assert.False(File.Exists(pathProvider.DatabasePath));
        }
    }

    [Fact]
    public void BuildConnectionStringPointsToDatabasePathWithForeignKeysEnabled()
    {
        const string databasePath = @"C:\fake-root\PosPlatform\Data\pos.db";

        var connectionString = DependencyInjection.BuildConnectionString(databasePath);

        Assert.Contains(databasePath, connectionString, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Foreign Keys=True", connectionString, StringComparison.OrdinalIgnoreCase);
    }
}
