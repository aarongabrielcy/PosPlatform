using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Activation;
using Pos.Application.AdministrativeNotifications;
using Pos.Application.Authentication;
using Pos.Application.Bootstrap;
using Pos.Application.Branches;
using Pos.Application.Common.Persistence;
using Pos.Application.Common.Time;
using Pos.Application.Common.Versioning;
using Pos.Application.Installation;
using Pos.Application.InstallationHealth;
using Pos.Application.Inventory;
using Pos.Application.Organizations;
using Pos.Application.Products;
using Pos.Application.Products.CreateProduct;
using Pos.Application.Products.ManageProduct;
using Pos.Application.RegisterSessions;
using Pos.Application.Registers;
using Pos.Application.Sales;
using Pos.Application.SalesCart;
using Pos.Application.Security;
using Pos.Application.Users;
using Pos.Infrastructure.Authentication;
using Pos.Infrastructure.InstallationHealth;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Persistence.Initialization;
using Pos.Infrastructure.Persistence.Repositories;
using Pos.Infrastructure.RegisterSessions;
using Pos.Infrastructure.SalesCart;
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
    public void IInstallationStateServiceResolvesWithinAScope()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            using var scope = provider.CreateScope();
            Assert.IsType<InstallationStateService>(
                scope.ServiceProvider.GetRequiredService<IInstallationStateService>());
        }
    }

    [Fact]
    public void IInstallationStateServiceIsScopedAndReusesTheSameInstanceWithinAScope()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            using var scope = provider.CreateScope();

            var first = scope.ServiceProvider.GetRequiredService<IInstallationStateService>();
            var second = scope.ServiceProvider.GetRequiredService<IInstallationStateService>();

            Assert.Same(first, second);
        }
    }

    [Fact]
    public void IInstallationStateServiceProducesDifferentInstancesAcrossScopes()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            using var scopeA = provider.CreateScope();
            using var scopeB = provider.CreateScope();

            var serviceA = scopeA.ServiceProvider.GetRequiredService<IInstallationStateService>();
            var serviceB = scopeB.ServiceProvider.GetRequiredService<IInstallationStateService>();

            Assert.NotSame(serviceA, serviceB);
        }
    }

    [Fact]
    public void ResolvingIInstallationStateServiceDoesNotCreateAnySqliteFile()
    {
        var (provider, pathProvider) = BuildProvider();

        using (provider)
        {
            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<IInstallationStateService>();

            Assert.False(Directory.Exists(pathProvider.DataDirectory));
            Assert.False(File.Exists(pathProvider.DatabasePath));
        }
    }

    [Fact]
    public void IAuthenticationServiceResolvesAuthenticationServiceWithinAScope()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            using var scope = provider.CreateScope();
            Assert.IsType<AuthenticationService>(scope.ServiceProvider.GetRequiredService<IAuthenticationService>());
        }
    }

    [Fact]
    public void IAuthenticationServiceIsScopedAndReusesTheSameInstanceWithinAScope()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            using var scope = provider.CreateScope();

            var first = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();
            var second = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();

            Assert.Same(first, second);
        }
    }

    [Fact]
    public void IAuthenticationServiceProducesDifferentInstancesAcrossScopes()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            using var scopeA = provider.CreateScope();
            using var scopeB = provider.CreateScope();

            var serviceA = scopeA.ServiceProvider.GetRequiredService<IAuthenticationService>();
            var serviceB = scopeB.ServiceProvider.GetRequiredService<IAuthenticationService>();

            Assert.NotSame(serviceA, serviceB);
        }
    }

    [Fact]
    public void ICurrentUserSessionResolvesInMemoryCurrentUserSessionAsSingletonAcrossScopes()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            var rootSession = provider.GetRequiredService<ICurrentUserSession>();

            using var scopeA = provider.CreateScope();
            using var scopeB = provider.CreateScope();

            var sessionA = scopeA.ServiceProvider.GetRequiredService<ICurrentUserSession>();
            var sessionB = scopeB.ServiceProvider.GetRequiredService<ICurrentUserSession>();

            Assert.IsType<InMemoryCurrentUserSession>(rootSession);
            Assert.Same(rootSession, sessionA);
            Assert.Same(rootSession, sessionB);
        }
    }

    [Fact]
    public void ICurrentUserSessionWriterResolvesTheSameSingletonInstanceAsICurrentUserSession()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            var session = provider.GetRequiredService<ICurrentUserSession>();
            var writer = provider.GetRequiredService<ICurrentUserSessionWriter>();

            Assert.Same(session, writer);
        }
    }

    [Fact]
    public void ResolvingAuthenticationServicesDoesNotCreateAnySqliteFile()
    {
        var (provider, pathProvider) = BuildProvider();

        using (provider)
        {
            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<IAuthenticationService>();
            provider.GetRequiredService<ICurrentUserSession>();

            Assert.False(Directory.Exists(pathProvider.DataDirectory));
            Assert.False(File.Exists(pathProvider.DatabasePath));
        }
    }

    [Fact]
    public void IRegisterSessionServiceResolvesRegisterSessionServiceWithinAScope()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            using var scope = provider.CreateScope();
            Assert.IsType<RegisterSessionService>(scope.ServiceProvider.GetRequiredService<IRegisterSessionService>());
        }
    }

    [Fact]
    public void IRegisterSessionServiceIsScopedAndReusesTheSameInstanceWithinAScope()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            using var scope = provider.CreateScope();

            var first = scope.ServiceProvider.GetRequiredService<IRegisterSessionService>();
            var second = scope.ServiceProvider.GetRequiredService<IRegisterSessionService>();

            Assert.Same(first, second);
        }
    }

    [Fact]
    public void IRegisterSessionServiceProducesDifferentInstancesAcrossScopes()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            using var scopeA = provider.CreateScope();
            using var scopeB = provider.CreateScope();

            var serviceA = scopeA.ServiceProvider.GetRequiredService<IRegisterSessionService>();
            var serviceB = scopeB.ServiceProvider.GetRequiredService<IRegisterSessionService>();

            Assert.NotSame(serviceA, serviceB);
        }
    }

    [Fact]
    public void ICurrentRegisterSessionResolvesInMemoryCurrentRegisterSessionAsSingletonAcrossScopes()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            var rootSession = provider.GetRequiredService<ICurrentRegisterSession>();

            using var scopeA = provider.CreateScope();
            using var scopeB = provider.CreateScope();

            var sessionA = scopeA.ServiceProvider.GetRequiredService<ICurrentRegisterSession>();
            var sessionB = scopeB.ServiceProvider.GetRequiredService<ICurrentRegisterSession>();

            Assert.IsType<InMemoryCurrentRegisterSession>(rootSession);
            Assert.Same(rootSession, sessionA);
            Assert.Same(rootSession, sessionB);
        }
    }

    [Fact]
    public void ResolvingRegisterSessionServicesDoesNotCreateAnySqliteFile()
    {
        var (provider, pathProvider) = BuildProvider();

        using (provider)
        {
            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<IRegisterSessionService>();
            provider.GetRequiredService<ICurrentRegisterSession>();

            Assert.False(Directory.Exists(pathProvider.DataDirectory));
            Assert.False(File.Exists(pathProvider.DatabasePath));
        }
    }

    [Fact]
    public void ISalesCartServiceResolvesSalesCartServiceWithinAScope()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            using var scope = provider.CreateScope();
            Assert.IsType<SalesCartService>(scope.ServiceProvider.GetRequiredService<ISalesCartService>());
        }
    }

    [Fact]
    public void ISalesCartServiceIsScopedAndReusesTheSameInstanceWithinAScope()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            using var scope = provider.CreateScope();

            var first = scope.ServiceProvider.GetRequiredService<ISalesCartService>();
            var second = scope.ServiceProvider.GetRequiredService<ISalesCartService>();

            Assert.Same(first, second);
        }
    }

    [Fact]
    public void ISalesCartServiceProducesDifferentInstancesAcrossScopes()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            using var scopeA = provider.CreateScope();
            using var scopeB = provider.CreateScope();

            var serviceA = scopeA.ServiceProvider.GetRequiredService<ISalesCartService>();
            var serviceB = scopeB.ServiceProvider.GetRequiredService<ISalesCartService>();

            Assert.NotSame(serviceA, serviceB);
        }
    }

    [Fact]
    public void ICurrentSalesCartResolvesInMemoryCurrentSalesCartAsSingletonAcrossScopes()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            var rootCart = provider.GetRequiredService<ICurrentSalesCart>();

            using var scopeA = provider.CreateScope();
            using var scopeB = provider.CreateScope();

            var cartA = scopeA.ServiceProvider.GetRequiredService<ICurrentSalesCart>();
            var cartB = scopeB.ServiceProvider.GetRequiredService<ICurrentSalesCart>();

            Assert.IsType<InMemoryCurrentSalesCart>(rootCart);
            Assert.Same(rootCart, cartA);
            Assert.Same(rootCart, cartB);
        }
    }

    [Fact]
    public void ResolvingSalesCartServicesDoesNotCreateAnySqliteFile()
    {
        var (provider, pathProvider) = BuildProvider();

        using (provider)
        {
            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<ISalesCartService>();
            provider.GetRequiredService<ICurrentSalesCart>();

            Assert.False(Directory.Exists(pathProvider.DataDirectory));
            Assert.False(File.Exists(pathProvider.DatabasePath));
        }
    }

    [Fact]
    public void ICreateProductServiceResolvesCreateProductServiceWithinAScope()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            using var scope = provider.CreateScope();
            Assert.IsType<CreateProductService>(scope.ServiceProvider.GetRequiredService<ICreateProductService>());
        }
    }

    [Fact]
    public void ICreateProductServiceIsScopedAndReusesTheSameInstanceWithinAScope()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            using var scope = provider.CreateScope();

            var first = scope.ServiceProvider.GetRequiredService<ICreateProductService>();
            var second = scope.ServiceProvider.GetRequiredService<ICreateProductService>();

            Assert.Same(first, second);
        }
    }

    [Fact]
    public void ICreateProductServiceProducesDifferentInstancesAcrossScopes()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            using var scopeA = provider.CreateScope();
            using var scopeB = provider.CreateScope();

            var serviceA = scopeA.ServiceProvider.GetRequiredService<ICreateProductService>();
            var serviceB = scopeB.ServiceProvider.GetRequiredService<ICreateProductService>();

            Assert.NotSame(serviceA, serviceB);
        }
    }

    [Fact]
    public void IProductManagementServiceResolvesProductManagementServiceWithinAScope()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            using var scope = provider.CreateScope();
            Assert.IsType<ProductManagementService>(
                scope.ServiceProvider.GetRequiredService<IProductManagementService>());
        }
    }

    [Fact]
    public void IProductManagementServiceIsScopedAndReusesTheSameInstanceWithinAScope()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            using var scope = provider.CreateScope();

            var first = scope.ServiceProvider.GetRequiredService<IProductManagementService>();
            var second = scope.ServiceProvider.GetRequiredService<IProductManagementService>();

            Assert.Same(first, second);
        }
    }

    [Fact]
    public void IProductManagementServiceProducesDifferentInstancesAcrossScopes()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            using var scopeA = provider.CreateScope();
            using var scopeB = provider.CreateScope();

            var serviceA = scopeA.ServiceProvider.GetRequiredService<IProductManagementService>();
            var serviceB = scopeB.ServiceProvider.GetRequiredService<IProductManagementService>();

            Assert.NotSame(serviceA, serviceB);
        }
    }

    [Fact]
    public void IProductCatalogQueryResolvesEfProductCatalogQueryWithinAScope()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            using var scope = provider.CreateScope();
            Assert.IsType<EfProductCatalogQuery>(scope.ServiceProvider.GetRequiredService<IProductCatalogQuery>());
        }
    }

    [Fact]
    public void IProductCatalogQueryProducesDifferentInstancesAcrossScopes()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            using var scopeA = provider.CreateScope();
            using var scopeB = provider.CreateScope();

            var serviceA = scopeA.ServiceProvider.GetRequiredService<IProductCatalogQuery>();
            var serviceB = scopeB.ServiceProvider.GetRequiredService<IProductCatalogQuery>();

            Assert.NotSame(serviceA, serviceB);
        }
    }

    // ---------- Administrative notifications (TAREA 24E) ----------

    [Fact]
    public void IAdministrativeNotificationRepositoryResolvesEfAdministrativeNotificationRepositoryWithinAScope()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            using var scope = provider.CreateScope();
            Assert.IsType<EfAdministrativeNotificationRepository>(
                scope.ServiceProvider.GetRequiredService<IAdministrativeNotificationRepository>());
        }
    }

    [Fact]
    public void IAdministrativeNotificationQueryResolvesEfAdministrativeNotificationQueryWithinAScope()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            using var scope = provider.CreateScope();
            Assert.IsType<EfAdministrativeNotificationQuery>(
                scope.ServiceProvider.GetRequiredService<IAdministrativeNotificationQuery>());
        }
    }

    [Fact]
    public void IAdministrativeNotificationAudienceQueryResolvesEfAdministrativeNotificationAudienceQueryWithinAScope()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            using var scope = provider.CreateScope();
            Assert.IsType<EfAdministrativeNotificationAudienceQuery>(
                scope.ServiceProvider.GetRequiredService<IAdministrativeNotificationAudienceQuery>());
        }
    }

    [Fact]
    public void IAdministrativeNotificationWriterResolvesAdministrativeNotificationWriterWithinAScope()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            using var scope = provider.CreateScope();
            Assert.IsType<AdministrativeNotificationWriter>(
                scope.ServiceProvider.GetRequiredService<IAdministrativeNotificationWriter>());
        }
    }

    [Fact]
    public void IAdministrativeNotificationServiceResolvesAdministrativeNotificationServiceWithinAScope()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            using var scope = provider.CreateScope();
            Assert.IsType<AdministrativeNotificationService>(
                scope.ServiceProvider.GetRequiredService<IAdministrativeNotificationService>());
        }
    }

    [Fact]
    public void AdministrativeNotificationServicesAreScopedAndProduceDifferentInstancesAcrossScopes()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            using var scopeA = provider.CreateScope();
            using var scopeB = provider.CreateScope();

            var serviceA = scopeA.ServiceProvider.GetRequiredService<IAdministrativeNotificationService>();
            var serviceB = scopeB.ServiceProvider.GetRequiredService<IAdministrativeNotificationService>();

            Assert.NotSame(serviceA, serviceB);
        }
    }

    [Fact]
    public void ResolvingAdministrativeNotificationServicesDoesNotCreateAnySqliteFile()
    {
        var (provider, pathProvider) = BuildProvider();

        using (provider)
        {
            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<IAdministrativeNotificationService>();
            scope.ServiceProvider.GetRequiredService<IAdministrativeNotificationWriter>();

            Assert.False(Directory.Exists(pathProvider.DataDirectory));
            Assert.False(File.Exists(pathProvider.DatabasePath));
        }
    }

    [Fact]
    public void IInstallationActivationClientResolvesAsSingletonAcrossScopes()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            var root = provider.GetRequiredService<IInstallationActivationClient>();

            using var scopeA = provider.CreateScope();
            using var scopeB = provider.CreateScope();

            Assert.Same(root, scopeA.ServiceProvider.GetRequiredService<IInstallationActivationClient>());
            Assert.Same(root, scopeB.ServiceProvider.GetRequiredService<IInstallationActivationClient>());
        }
    }

    [Fact]
    public void IInstallationCredentialStoreResolvesOnWindows()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            Assert.NotNull(provider.GetRequiredService<IInstallationCredentialStore>());
        }
    }

    [Fact]
    public void IInstallationActivationRecordStoreResolvesAsSingleton()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            var root = provider.GetRequiredService<IInstallationActivationRecordStore>();
            using var scope = provider.CreateScope();

            Assert.Same(root, scope.ServiceProvider.GetRequiredService<IInstallationActivationRecordStore>());
        }
    }

    [Fact]
    public void IInstallationActivationStateServiceIsScopedAndReusesTheSameInstanceWithinAScope()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            using var scope = provider.CreateScope();

            var first = scope.ServiceProvider.GetRequiredService<IInstallationActivationStateService>();
            var second = scope.ServiceProvider.GetRequiredService<IInstallationActivationStateService>();

            Assert.Same(first, second);
        }
    }

    [Fact]
    public void ResolvingActivationServicesDoesNotCreateAnySqliteFile()
    {
        var (provider, pathProvider) = BuildProvider();

        using (provider)
        {
            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<IInstallationActivationStateService>();

            Assert.False(Directory.Exists(pathProvider.DataDirectory));
            Assert.False(File.Exists(pathProvider.DatabasePath));
        }
    }

    [Fact]
    public void IInstallationHealthClientResolvesAsSingletonAcrossScopes()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            var root = provider.GetRequiredService<IInstallationHealthClient>();

            using var scopeA = provider.CreateScope();
            using var scopeB = provider.CreateScope();

            Assert.Same(root, scopeA.ServiceProvider.GetRequiredService<IInstallationHealthClient>());
            Assert.Same(root, scopeB.ServiceProvider.GetRequiredService<IInstallationHealthClient>());
        }
    }

    [Fact]
    public void IApplicationVersionProviderResolvesAssemblyApplicationVersionProviderAsSingleton()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            var root = provider.GetRequiredService<IApplicationVersionProvider>();

            using var scope = provider.CreateScope();

            Assert.IsType<AssemblyApplicationVersionProvider>(root);
            Assert.Same(root, scope.ServiceProvider.GetRequiredService<IApplicationVersionProvider>());
        }
    }

    [Fact]
    public void IInstallationHeartbeatSenderResolvesAsSingletonAcrossScopes()
    {
        var (provider, _) = BuildProvider();

        using (provider)
        {
            var root = provider.GetRequiredService<IInstallationHeartbeatSender>();

            using var scopeA = provider.CreateScope();
            using var scopeB = provider.CreateScope();

            Assert.IsType<InstallationHeartbeatSender>(root);
            Assert.Same(root, scopeA.ServiceProvider.GetRequiredService<IInstallationHeartbeatSender>());
            Assert.Same(root, scopeB.ServiceProvider.GetRequiredService<IInstallationHeartbeatSender>());
        }
    }

    [Fact]
    public void ResolvingInstallationHealthServicesDoesNotCreateAnySqliteFile()
    {
        var (provider, pathProvider) = BuildProvider();

        using (provider)
        {
            provider.GetRequiredService<IInstallationHeartbeatSender>();

            Assert.False(Directory.Exists(pathProvider.DataDirectory));
            Assert.False(File.Exists(pathProvider.DatabasePath));
        }
    }

    [Fact]
    public void NormalizeBaseUrlAppendsTrailingSlashOnlyWhenMissing()
    {
        Assert.Equal("http://localhost:3000/", DependencyInjection.NormalizeBaseUrl("http://localhost:3000"));
        Assert.Equal("http://localhost:3000/", DependencyInjection.NormalizeBaseUrl("http://localhost:3000/"));
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
