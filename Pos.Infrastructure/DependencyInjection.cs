using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pos.Application.Activation;
using Pos.Application.AdministrativeNotifications;
using Pos.Application.Authentication;
using Pos.Application.Bootstrap;
using Pos.Application.Branches;
using Pos.Application.Common.Persistence;
using Pos.Application.Common.Time;
using Pos.Application.Installation;
using Pos.Application.Inventory;
using Pos.Application.Organizations;
using Pos.Application.ProductAudit;
using Pos.Application.Products;
using Pos.Application.Products.CreateProduct;
using Pos.Application.Products.ManageProduct;
using Pos.Application.RegisterSessions;
using Pos.Application.Registers;
using Pos.Application.Sales;
using Pos.Application.Sales.Checkout;
using Pos.Application.Sales.History;
using Pos.Application.SalesCart;
using Pos.Application.Security;
using Pos.Application.Users;
using Pos.Infrastructure.Activation;
using Pos.Infrastructure.Authentication;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Persistence.Initialization;
using Pos.Infrastructure.Persistence.Repositories;
using Pos.Infrastructure.RegisterSessions;
using Pos.Infrastructure.SalesCart;
using Pos.Infrastructure.Security;
using Pos.Infrastructure.Storage;
using Pos.Infrastructure.Time;

namespace Pos.Infrastructure;

public static class DependencyInjection
{
    // installationActivationBaseUrl: URL base de pos-cloud para el canje del Enrollment Code
    // (ver Pos.Desktop/appsettings.json, clave "Activation:BaseUrl"). El valor por defecto
    // apunta al backend local de desarrollo y solo se usa si no se provee configuración explícita
    // (p. ej. en las pruebas de composición existentes, que llaman a este método sin argumentos).
    public static IServiceCollection AddPosInfrastructure(
        this IServiceCollection services,
        string installationActivationBaseUrl = "http://localhost:5100")
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
        services.AddScoped<IInventoryCatalogQuery, EfInventoryCatalogQuery>();
        services.AddScoped<IInventoryMovementQuery, EfInventoryMovementQuery>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<ISaleRepository, EfSaleRepository>();
        services.AddScoped<ISalesHistoryQuery, EfSalesHistoryQuery>();
        services.AddScoped<ISalesHistoryService, SalesHistoryService>();
        services.AddScoped<IOrganizationRepository, EfOrganizationRepository>();
        services.AddScoped<IBranchRepository, EfBranchRepository>();
        services.AddScoped<IRegisterRepository, EfRegisterRepository>();
        services.AddScoped<IProductRepository, EfProductRepository>();
        services.AddScoped<IProductCatalogQuery, EfProductCatalogQuery>();
        services.AddScoped<IRoleRepository, EfRoleRepository>();
        services.AddScoped<IUserRepository, EfUserRepository>();
        services.AddScoped<IRegisterSessionRepository, EfRegisterSessionRepository>();
        services.AddScoped<ICreateProductService, CreateProductService>();
        services.AddScoped<IProductManagementService, ProductManagementService>();
        services.AddScoped<IProductAuditRepository, EfProductAuditRepository>();
        services.AddScoped<IProductAuditQuery, EfProductAuditQuery>();
        services.AddScoped<IProductAuditService, ProductAuditService>();

        services.AddScoped<IAdministrativeNotificationRepository, EfAdministrativeNotificationRepository>();
        services.AddScoped<IAdministrativeNotificationQuery, EfAdministrativeNotificationQuery>();
        services.AddScoped<IAdministrativeNotificationAudienceQuery, EfAdministrativeNotificationAudienceQuery>();
        services.AddScoped<IAdministrativeNotificationWriter, AdministrativeNotificationWriter>();
        services.AddScoped<IAdministrativeNotificationService, AdministrativeNotificationService>();

        services.AddScoped<IInitialBusinessBootstrapService, InitialBusinessBootstrapService>();
        services.AddScoped<IInstallationStateService, InstallationStateService>();

        services.AddSingleton<InMemoryCurrentUserSession>();
        services.AddSingleton<ICurrentUserSession>(sp => sp.GetRequiredService<InMemoryCurrentUserSession>());
        services.AddSingleton<ICurrentUserSessionWriter>(sp => sp.GetRequiredService<InMemoryCurrentUserSession>());
        services.AddScoped<IAuthenticationService, AuthenticationService>();

        services.AddSingleton<InMemoryCurrentRegisterSession>();
        services.AddSingleton<ICurrentRegisterSession>(sp => sp.GetRequiredService<InMemoryCurrentRegisterSession>());
        services.AddScoped<IRegisterSessionService, RegisterSessionService>();

        services.AddSingleton<InMemoryCurrentSalesCart>();
        services.AddSingleton<ICurrentSalesCart>(sp => sp.GetRequiredService<InMemoryCurrentSalesCart>());
        services.AddScoped<ISalesCartService, SalesCartService>();
        services.AddScoped<ICheckoutService, CheckoutService>();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<ILocalDatabaseInitializer, LocalDatabaseInitializer>();

        services.AddSingleton<IInstallationActivationClient>(sp =>
        {
            var httpClient = new HttpClient
            {
                BaseAddress = new Uri(NormalizeBaseUrl(installationActivationBaseUrl), UriKind.Absolute),
            };

            return ActivatorUtilities.CreateInstance<HttpInstallationActivationClient>(sp, httpClient);
        });

        // DpapiInstallationCredentialStore usa CryptProtectData/CryptUnprotectData (DPAPI), una
        // API exclusiva de Windows. PosPlatform Desktop es net8.0-windows/WPF, por lo que esta
        // condición siempre es verdadera en producción; se expresa como guard explícito (en vez de
        // suprimir CA1416) porque es el patrón que el analizador de compatibilidad reconoce.
        if (OperatingSystem.IsWindows())
        {
            services.AddSingleton<IInstallationCredentialStore, DpapiInstallationCredentialStore>();
        }

        services.AddSingleton<IInstallationActivationRecordStore, FileInstallationActivationRecordStore>();
        services.AddScoped<IInstallationActivationStateService, InstallationActivationStateService>();

        return services;
    }

    // HttpClient exige que BaseAddress termine en "/" para que las rutas relativas sin "/" inicial
    // (p. ej. "api/v1/installation-auth/enroll") se combinen agregando el segmento en lugar de
    // reemplazar el último tramo de la URL configurada.
    internal static string NormalizeBaseUrl(string baseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        return baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/";
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
