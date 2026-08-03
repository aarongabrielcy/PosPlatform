using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Authentication;
using Pos.Application.Products.CreateProduct;
using Pos.Application.RegisterSessions;
using Pos.Application.SalesCart;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;
using Pos.Infrastructure.Authentication;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Persistence.Records;
using Pos.Infrastructure.Persistence.Repositories;
using Pos.Infrastructure.RegisterSessions;
using Pos.Infrastructure.SalesCart;
using Pos.Infrastructure.Tests.Persistence;
using Pos.Infrastructure.Time;

namespace Pos.Infrastructure.Tests.Integration;

// Ejercita CreateProductService contra un SQLite real (bootstrap -> login -> caja abierta) para
// confirmar que el producto y su InventoryItem inicial quedan persistidos en la misma operación y
// que SalesCartService.SearchProductsAsync encuentra el producto recién creado de inmediato.
public class CreateProductSqliteIntegrationTests
{
    private static readonly DateTimeOffset DefaultTimestamp = SqliteSeedHelper.DefaultTimestamp;

    private static PosDbContext CreateContext(SqliteConnection connection)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PosDbContext>();
        optionsBuilder.UseSqlite(connection);

        return new PosDbContext(optionsBuilder.Options);
    }

    private static async Task<(
        PosDbContext Context,
        CreateProductService Service,
        SalesCartService CartService,
        SqliteSeedHelper.CatalogGraph Graph)> CreateAuthenticatedServiceAsync(SqliteConnection connection)
    {
        var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context, includeProduct: false);

        var userSession = new InMemoryCurrentUserSession();
        userSession.SetAuthenticatedUser(new AuthenticatedUser(
            new UserId(graph.UserId),
            new OrganizationId(graph.OrganizationId),
            new RoleId(graph.RoleId),
            "JPEREZ",
            "Juan Pérez",
            "Gerente",
            new HashSet<Permission> { Permission.ManageProducts }));

        var registerSession = new InMemoryCurrentRegisterSession();
        registerSession.SetActiveSession(new ActiveRegisterSession(
            new RegisterSessionId(graph.RegisterSessionId),
            new OrganizationId(graph.OrganizationId),
            new BranchId(graph.BranchId),
            new RegisterId(graph.RegisterId),
            "Caja 1",
            new UserId(graph.UserId),
            "Juan Pérez",
            DefaultTimestamp,
            100m,
            "MXN"));

        var productRepository = new EfProductRepository(context);
        var inventoryItemRepository = new EfInventoryItemRepository(context);
        var productAuditRepository = new EfProductAuditRepository(context);
        var clock = new SystemClock();

        var createProductService = new CreateProductService(
            userSession, registerSession, productRepository, inventoryItemRepository, productAuditRepository,
            context, clock);

        var cartService = new SalesCartService(
            userSession, registerSession, new InMemoryCurrentSalesCart(), productRepository, inventoryItemRepository);

        return (context, createProductService, cartService, graph);
    }

    [Fact]
    public async Task CreateAsyncPersistsProductAndInventoryItemInASingleCommit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        var (context, service, cartService, graph) = await CreateAuthenticatedServiceAsync(connection);
        await using var contextDisposable = context;

        var request = new CreateProductRequest(
            "SKU-NEW", "7501234567890", "Producto nuevo", "Descripción", 25m, 10m, true, 8m, 2m);

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("SKU-NEW", result.Sku);

        var productRecord = await context.Set<ProductRecord>().AsNoTracking()
            .SingleAsync(r => r.Id == result.ProductId!.Value.Value);
        Assert.Equal("Producto nuevo", productRecord.Name);
        Assert.Equal(25m, productRecord.SalePriceAmount);

        var inventoryRecord = await context.Set<InventoryItemRecord>().AsNoTracking()
            .SingleAsync(r => r.ProductId == result.ProductId!.Value.Value);
        Assert.Equal(8m, inventoryRecord.Quantity);
        Assert.Equal(2m, inventoryRecord.ReorderPoint);
        Assert.Equal(graph.BranchId, inventoryRecord.BranchId);
    }

    [Fact]
    public async Task CreateAsyncWithoutTracksInventoryDoesNotCreateInventoryItem()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        var (context, service, _, _) = await CreateAuthenticatedServiceAsync(connection);
        await using var contextDisposable = context;

        var request = new CreateProductRequest(
            "SKU-NO-INV", null, "Producto sin inventario", null, 15m, null, false, 0m, 0m);

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.True(result.Success);

        var inventoryCount = await context.Set<InventoryItemRecord>()
            .CountAsync(r => r.ProductId == result.ProductId!.Value.Value);
        Assert.Equal(0, inventoryCount);
    }

    [Fact]
    public async Task CreateAsyncRejectsDuplicateSkuWithinOrganization()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        var (context, service, _, _) = await CreateAuthenticatedServiceAsync(connection);
        await using var contextDisposable = context;

        var request = new CreateProductRequest(
            "SKU-DUP", null, "Producto original", null, 10m, null, false, 0m, 0m);
        Assert.True((await service.CreateAsync(request, CancellationToken.None)).Success);

        var duplicateRequest = new CreateProductRequest(
            "sku-dup", null, "Producto duplicado", null, 10m, null, false, 0m, 0m);
        var duplicateResult = await service.CreateAsync(duplicateRequest, CancellationToken.None);

        Assert.False(duplicateResult.Success);
        Assert.Equal(CreateProductResultStatus.DuplicateSku, duplicateResult.Status);
    }

    [Fact]
    public async Task SearchProductsAsyncFindsProductImmediatelyAfterCreation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        var (context, service, cartService, _) = await CreateAuthenticatedServiceAsync(connection);
        await using var contextDisposable = context;

        var request = new CreateProductRequest(
            "SKU-FIND", null, "Producto localizable", null, 30m, null, true, 5m, 1m);
        var createResult = await service.CreateAsync(request, CancellationToken.None);
        Assert.True(createResult.Success);

        var searchResults = await cartService.SearchProductsAsync("SKU-FIND");

        Assert.Single(searchResults);
        Assert.Equal(5m, searchResults[0].AvailableQuantity);
    }
}
