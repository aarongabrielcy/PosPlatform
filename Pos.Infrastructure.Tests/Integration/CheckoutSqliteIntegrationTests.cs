using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Application.AdministrativeNotifications;
using Pos.Application.Authentication;
using Pos.Application.Products.ManageProduct;
using Pos.Application.RegisterSessions;
using Pos.Application.Sales.Checkout;
using Pos.Application.SalesCart;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Inventory;
using Pos.Domain.Sales;
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

// Checkout end-to-end contra SQLite real (TAREA 25A sección 28): sesión de caja abierta, producto
// con inventario sembrados por fixtures existentes, carrito en memoria, cobro en efectivo. Verifica
// que Sale/SaleLine/Payment/InventoryItem/InventoryMovement queden persistidos en un único commit y
// que el carrito quede vacío solo después de esa persistencia.
public class CheckoutSqliteIntegrationTests
{
    private static PosDbContext CreateContext(SqliteConnection connection)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PosDbContext>();
        optionsBuilder.UseSqlite(connection);

        return new PosDbContext(optionsBuilder.Options);
    }

    private static async Task<(
        PosDbContext Context,
        CheckoutService Service,
        InMemoryCurrentSalesCart Cart,
        SqliteSeedHelper.CatalogGraph Graph,
        InMemoryCurrentUserSession UserSession,
        InMemoryCurrentRegisterSession RegisterSession)> CreateAuthenticatedServiceAsync(
        SqliteConnection connection, decimal inventoryQuantity = 10m)
    {
        var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context);
        await SqliteSeedHelper.SeedInventoryItemAsync(context, graph.BranchId, graph.ProductId, quantity: inventoryQuantity);

        var userSession = new InMemoryCurrentUserSession();
        userSession.SetAuthenticatedUser(new AuthenticatedUser(
            new UserId(graph.UserId),
            new OrganizationId(graph.OrganizationId),
            new RoleId(graph.RoleId),
            "JPEREZ",
            "Juan Pérez",
            "Cajero",
            [Permission.ProcessSale, Permission.AdjustInventory]));

        var registerSession = new InMemoryCurrentRegisterSession();
        registerSession.SetActiveSession(new ActiveRegisterSession(
            new RegisterSessionId(graph.RegisterSessionId),
            new OrganizationId(graph.OrganizationId),
            new BranchId(graph.BranchId),
            new RegisterId(graph.RegisterId),
            "Caja 1",
            new UserId(graph.UserId),
            "Juan Pérez",
            SqliteSeedHelper.DefaultTimestamp,
            100m,
            "MXN"));

        var cart = new InMemoryCurrentSalesCart();
        cart.SetSnapshot(new SalesCartSnapshot(
            [new SalesCartLine(new ProductId(graph.ProductId), "SKU-001", "Producto de prueba", 2m, 10m, 20m, "MXN", inventoryQuantity, true)],
            "MXN"));

        var productRepository = new EfProductRepository(context);
        var inventoryItemRepository = new EfInventoryItemRepository(context);
        var inventoryMovementRepository = new EfInventoryMovementRepository(context);
        var saleRepository = new EfSaleRepository(context);

        var service = new CheckoutService(
            userSession, registerSession, cart, productRepository, inventoryItemRepository,
            inventoryMovementRepository, saleRepository, new Enforcement.FakeInstallationEnforcementStateService(),
            context, new SystemClock());

        return (context, service, cart, graph, userSession, registerSession);
    }

    [Fact]
    public async Task SuccessfulCashCheckoutPersistsEverythingInASingleCommitAndClearsTheCart()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        var (context, service, cart, graph, _, _) = await CreateAuthenticatedServiceAsync(connection);
        await using var contextDisposable = context;

        var result = await service.CheckoutAsync(new CheckoutRequest(20m));

        Assert.True(result.Success);
        Assert.Equal(20m, result.Summary!.TotalAmount);
        Assert.Equal(0m, result.Summary.ChangeAmount);
        Assert.Empty(cart.Snapshot.Lines);

        var saleRecord = await context.Set<SaleRecord>()
            .AsNoTracking()
            .Include(r => r.Lines)
            .Include(r => r.Payments)
            .SingleAsync(r => r.Id == result.Summary.SaleId);

        Assert.Equal(SaleStatus.Completed, saleRecord.Status);
        Assert.NotNull(saleRecord.CompletedAtUtc);
        Assert.Equal(graph.RegisterSessionId, saleRecord.RegisterSessionId);
        Assert.Equal(graph.OrganizationId, saleRecord.OrganizationId);
        Assert.Equal(graph.BranchId, saleRecord.BranchId);
        Assert.Equal(graph.UserId, saleRecord.CreatedByUserId);

        var line = Assert.Single(saleRecord.Lines);
        Assert.Equal(graph.ProductId, line.ProductId);
        Assert.Equal(2m, line.Quantity);

        var payment = Assert.Single(saleRecord.Payments);
        Assert.Equal(PaymentMethod.Cash, payment.Method);
        Assert.Equal(20m, payment.Amount);

        var inventoryItem = await context.Set<InventoryItemRecord>()
            .AsNoTracking()
            .SingleAsync(r => r.BranchId == graph.BranchId && r.ProductId == graph.ProductId);
        Assert.Equal(8m, inventoryItem.Quantity);

        var movement = await context.Set<InventoryMovementRecord>()
            .AsNoTracking()
            .SingleAsync(r => r.ProductId == graph.ProductId);
        Assert.Equal(InventoryMovementType.SaleDecrease, movement.Type);
        Assert.Equal(10m, movement.QuantityBefore);
        Assert.Equal(8m, movement.QuantityAfter);
        Assert.Equal(saleRecord.Id, movement.SaleId);
        Assert.Equal(line.Id, movement.SaleLineId);
    }

    // TAREA 25C: Manual Card (terminal externa ajena a PosPlatform). El monto persistido es
    // exactamente el total (nunca hay CashTendered/Change) y la referencia/autorización queda en
    // Payment.Reference, nunca datos sensibles de tarjeta (no se piden en ningún punto del flujo).
    [Fact]
    public async Task SuccessfulManualCardCheckoutPersistsReferenceAndExactAmount()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        var (context, service, cart, graph, _, _) = await CreateAuthenticatedServiceAsync(connection);
        await using var contextDisposable = context;

        var result = await service.CheckoutAsync(
            new CheckoutRequest(0m, CheckoutPaymentMethod.Card, "  AUTH-77321  "));

        Assert.True(result.Success);
        Assert.Equal(20m, result.Summary!.TotalAmount);
        Assert.Equal(CheckoutPaymentMethod.Card, result.Summary.PaymentMethod);
        Assert.Equal(0m, result.Summary.CashTendered);
        Assert.Equal(0m, result.Summary.ChangeAmount);
        Assert.Equal("AUTH-77321", result.Summary.CardReference);
        Assert.Empty(cart.Snapshot.Lines);

        var saleRecord = await context.Set<SaleRecord>()
            .AsNoTracking()
            .Include(r => r.Payments)
            .SingleAsync(r => r.Id == result.Summary.SaleId);

        Assert.Equal(SaleStatus.Completed, saleRecord.Status);

        var payment = Assert.Single(saleRecord.Payments);
        Assert.Equal(PaymentMethod.Card, payment.Method);
        Assert.Equal(20m, payment.Amount);
        Assert.Equal("AUTH-77321", payment.Reference);
    }

    [Fact]
    public async Task ManualCardCheckoutWithBlankReferenceIsRejectedAndPersistsNothing()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        var (context, service, cart, _, _, _) = await CreateAuthenticatedServiceAsync(connection);
        await using var contextDisposable = context;

        var result = await service.CheckoutAsync(new CheckoutRequest(0m, CheckoutPaymentMethod.Card, "   "));

        Assert.Equal(CheckoutResultStatus.InvalidCardReference, result.Status);
        Assert.Single(cart.Snapshot.Lines);
        Assert.Equal(0, await context.Set<SaleRecord>().CountAsync());
        Assert.Equal(0, await context.Set<PaymentRecord>().CountAsync());
    }

    [Fact]
    public async Task InsufficientStockCheckoutPersistsNothingAndKeepsTheCart()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        var (context, service, cart, graph, _, _) = await CreateAuthenticatedServiceAsync(connection, inventoryQuantity: 1m);
        await using var contextDisposable = context;

        var result = await service.CheckoutAsync(new CheckoutRequest(20m));

        Assert.Equal(CheckoutResultStatus.InsufficientStock, result.Status);
        Assert.Equal(1m, result.AvailableQuantity);
        Assert.Single(cart.Snapshot.Lines);

        Assert.Equal(0, await context.Set<SaleRecord>().CountAsync());
        Assert.Equal(0, await context.Set<InventoryMovementRecord>().CountAsync());

        var inventoryItem = await context.Set<InventoryItemRecord>()
            .AsNoTracking()
            .SingleAsync(r => r.BranchId == graph.BranchId && r.ProductId == graph.ProductId);
        Assert.Equal(1m, inventoryItem.Quantity);
    }

    // TAREA 25A-FIX sección 13: escenario manual exacto que el usuario quería probar y no pudo
    // reproducir por el defecto 2 (inventario a cero). El producto queda en el carrito con stock=2
    // reservado; un administrador ajusta el inventario a 0 con ProductManagementService mientras la
    // venta sigue en el carrito, y el cobro debe rechazarse sin tocar Sale/Payment/inventario/carrito.
    [Fact]
    public async Task CheckoutFailsWithInsufficientStockAfterInventoryIsAdjustedToZeroWhileInTheCart()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        var (context, service, cart, graph, userSession, registerSession) =
            await CreateAuthenticatedServiceAsync(connection, inventoryQuantity: 2m);
        await using var contextDisposable = context;

        var managementServiceClock = new SystemClock();
        var managementService = new ProductManagementService(
            userSession,
            registerSession,
            new EfProductRepository(context),
            new EfInventoryItemRepository(context),
            new EfInventoryMovementRepository(context),
            new EfProductCatalogQuery(context),
            new EfProductAuditRepository(context),
            new EfProductAuditQuery(context),
            new AdministrativeNotificationWriter(
                new EfAdministrativeNotificationAudienceQuery(context),
                new EfAdministrativeNotificationRepository(context),
                managementServiceClock),
            new Enforcement.FakeInstallationEnforcementStateService(),
            context,
            managementServiceClock);

        var adjustResult = await managementService.AdjustInventoryAsync(new AdjustProductInventoryRequest(
            new ProductId(graph.ProductId), InventoryAdjustmentType.Decrease, 2m));

        Assert.True(adjustResult.Success);
        Assert.Equal(0m, adjustResult.NewQuantity);

        var result = await service.CheckoutAsync(new CheckoutRequest(20m));

        Assert.Equal(CheckoutResultStatus.InsufficientStock, result.Status);
        Assert.Equal(0m, result.AvailableQuantity);
        Assert.Single(cart.Snapshot.Lines);

        Assert.Equal(0, await context.Set<SaleRecord>().CountAsync());
        Assert.Equal(0, await context.Set<PaymentRecord>().CountAsync());

        var inventoryItem = await context.Set<InventoryItemRecord>()
            .AsNoTracking()
            .SingleAsync(r => r.BranchId == graph.BranchId && r.ProductId == graph.ProductId);
        Assert.Equal(0m, inventoryItem.Quantity);
    }
}
