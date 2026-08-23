using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Application.AdministrativeNotifications;
using Pos.Application.Authentication;
using Pos.Application.Products.ManageProduct;
using Pos.Application.RegisterSessions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;
using Pos.Infrastructure.Authentication;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Persistence.Records;
using Pos.Infrastructure.Persistence.Repositories;
using Pos.Infrastructure.RegisterSessions;
using Pos.Infrastructure.Tests.Persistence;
using Pos.Infrastructure.Time;

namespace Pos.Infrastructure.Tests.Integration;

// Ejercita ProductPhotoService contra un SQLite real (BASIC-UX-01, sección 45/48): confirma que la
// migración additiva image_file_name persiste y recarga correctamente, sin tocar ninguna Sale ni
// requerir el resto del catálogo administrativo.
public class ProductPhotoSqliteIntegrationTests
{
    private static readonly DateTimeOffset DefaultTimestamp = SqliteSeedHelper.DefaultTimestamp;

    private static PosDbContext CreateContext(SqliteConnection connection)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PosDbContext>();
        optionsBuilder.UseSqlite(connection);

        return new PosDbContext(optionsBuilder.Options);
    }

    private static async Task<(PosDbContext Context, ProductPhotoService PhotoService, ProductId ProductId)>
        CreateServiceWithSeededProductAsync(SqliteConnection connection, Products.FakeProductImageStore imageStore)
    {
        var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context, includeProduct: false);

        var userSession = new InMemoryCurrentUserSession();
        userSession.SetAuthenticatedUser(new AuthenticatedUser(
            new UserId(graph.UserId), new OrganizationId(graph.OrganizationId), new RoleId(graph.RoleId),
            "JPEREZ", "Juan Pérez", "Gerente",
            new HashSet<Permission> { Permission.ManageProducts, Permission.ViewProducts }));

        var registerSession = new InMemoryCurrentRegisterSession();
        registerSession.SetActiveSession(new ActiveRegisterSession(
            new RegisterSessionId(graph.RegisterSessionId), new OrganizationId(graph.OrganizationId),
            new BranchId(graph.BranchId), new RegisterId(graph.RegisterId), "Caja 1",
            new UserId(graph.UserId), "Juan Pérez", DefaultTimestamp, 100m, "MXN"));

        var productRepository = new EfProductRepository(context);
        var inventoryItemRepository = new EfInventoryItemRepository(context);
        var clock = new SystemClock();

        var createService = new Pos.Application.Products.CreateProduct.CreateProductService(
            userSession, registerSession, productRepository, inventoryItemRepository,
            new EfProductAuditRepository(context), new Enforcement.FakeInstallationEnforcementStateService(),
            context, clock);

        var createResult = await createService.CreateAsync(
            new Pos.Application.Products.CreateProduct.CreateProductRequest(
                "SKU-PHOTO", null, "Producto con foto", null, 10m, null, false, 0m, 0m),
            CancellationToken.None);
        Assert.True(createResult.Success);

        var administrativeNotificationWriter = new AdministrativeNotificationWriter(
            new EfAdministrativeNotificationAudienceQuery(context), new EfAdministrativeNotificationRepository(context), clock);

        var photoService = new ProductPhotoService(
            userSession, registerSession, productRepository, inventoryItemRepository,
            new EfProductAuditRepository(context), administrativeNotificationWriter,
            new Enforcement.FakeInstallationEnforcementStateService(), context, clock, imageStore);

        return (context, photoService, createResult.ProductId!.Value);
    }

    [Fact]
    public async Task ExistingProductWithoutAPhotoPersistsANullImageFileName()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        var (context, _, productId) = await CreateServiceWithSeededProductAsync(connection, new Products.FakeProductImageStore());
        await using var contextDisposable = context;

        var record = await context.Set<ProductRecord>().AsNoTracking().SingleAsync(r => r.Id == productId.Value);

        Assert.Null(record.ImageFileName);
    }

    [Fact]
    public async Task SettingAPhotoPersistsTheManagedFileNameAndReloadsItFromANewContext()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        var (context, photoService, productId) = await CreateServiceWithSeededProductAsync(connection, new Products.FakeProductImageStore());
        await using var contextDisposable = context;

        var result = await photoService.SetProductImageAsync(productId, [1, 2, 3]);
        Assert.True(result.Success);

        var record = await context.Set<ProductRecord>().AsNoTracking().SingleAsync(r => r.Id == productId.Value);
        Assert.NotNull(record.ImageFileName);
        Assert.Equal(result.Product!.ImageFileName, record.ImageFileName);

        await using var reloadContext = CreateContext(connection);
        var reloadedRepository = new EfProductRepository(reloadContext);
        var reloadedProduct = await reloadedRepository.GetByIdAsync(productId, CancellationToken.None);

        Assert.NotNull(reloadedProduct);
        Assert.Equal(record.ImageFileName, reloadedProduct!.ImageFileName);
        Assert.Equal(productId, reloadedProduct.Id);
    }

    [Fact]
    public async Task RemovingAPhotoPersistsANullImageFileName()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        var (context, photoService, productId) = await CreateServiceWithSeededProductAsync(connection, new Products.FakeProductImageStore());
        await using var contextDisposable = context;

        await photoService.SetProductImageAsync(productId, [1, 2, 3]);
        var removeResult = await photoService.RemoveProductImageAsync(productId);
        Assert.True(removeResult.Success);

        var record = await context.Set<ProductRecord>().AsNoTracking().SingleAsync(r => r.Id == productId.Value);
        Assert.Null(record.ImageFileName);
    }
}
