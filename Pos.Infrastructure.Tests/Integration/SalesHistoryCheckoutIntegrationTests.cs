using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Authentication;
using Pos.Application.RegisterSessions;
using Pos.Application.Sales.Checkout;
using Pos.Application.Sales.History;
using Pos.Application.SalesCart;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;
using Pos.Infrastructure.Authentication;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Persistence.Repositories;
using Pos.Infrastructure.RegisterSessions;
using Pos.Infrastructure.SalesCart;
using Pos.Infrastructure.Tests.Persistence;
using Pos.Infrastructure.Time;

namespace Pos.Infrastructure.Tests.Integration;

// TAREA 25B-FIX, sección 15: valida el contrato completo Checkout write path -> EF persistence ->
// SalesHistory read path contra SQLite real, en vez de sembrar SaleRecord a mano. Reproduce el
// escenario reportado por el usuario: completar una venta con CheckoutService real y, de inmediato,
// verificar que aparece en SalesHistoryService bajo el filtro "Hoy" (mismo día local que el reloj
// real usa para CompletedAtUtc).
public class SalesHistoryCheckoutIntegrationTests
{
    private static PosDbContext CreateContext(SqliteConnection connection)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PosDbContext>();
        optionsBuilder.UseSqlite(connection);

        return new PosDbContext(optionsBuilder.Options);
    }

    private static DateTimeOffset ToStartOfDayUtc(DateTime localDate) =>
        new DateTimeOffset(localDate.Date, TimeZoneInfo.Local.GetUtcOffset(localDate.Date)).ToUniversalTime();

    private static DateTimeOffset ToExclusiveEndOfDayUtc(DateTime localDate)
    {
        var nextDay = localDate.Date.AddDays(1);

        return new DateTimeOffset(nextDay, TimeZoneInfo.Local.GetUtcOffset(nextDay)).ToUniversalTime();
    }

    [Fact]
    public async Task ASaleCompletedThroughCheckoutServiceAppearsInSalesHistoryUnderTodaysFilterImmediately()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        // 1-7: Organization/Branch/Register/User/RegisterSession abierta/Product/Inventory, mismo
        // grafo que usa CheckoutSqliteIntegrationTests.
        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context);
        await SqliteSeedHelper.SeedInventoryItemAsync(context, graph.BranchId, graph.ProductId, quantity: 10m);

        var userSession = new InMemoryCurrentUserSession();
        userSession.SetAuthenticatedUser(new AuthenticatedUser(
            new UserId(graph.UserId),
            new OrganizationId(graph.OrganizationId),
            new RoleId(graph.RoleId),
            "JPEREZ",
            "Juan Pérez",
            "Cajero",
            [Permission.ProcessSale, Permission.ViewReports, Permission.ViewSalesHistory]));

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
            [new SalesCartLine(new ProductId(graph.ProductId), "SKU-001", "Producto de prueba", 2m, 10m, 20m, "MXN", 10m, true)],
            "MXN"));

        var productRepository = new EfProductRepository(context);
        var inventoryItemRepository = new EfInventoryItemRepository(context);
        var inventoryMovementRepository = new EfInventoryMovementRepository(context);
        var saleRepository = new EfSaleRepository(context);

        // 8: ejecutar CheckoutService real (reloj real, no fake) para completar la venta.
        var checkoutService = new CheckoutService(
            userSession, registerSession, cart, productRepository, inventoryItemRepository,
            inventoryMovementRepository, saleRepository, new Enforcement.FakeInstallationEnforcementStateService(),
            context, new SystemClock());

        var checkoutResult = await checkoutService.CheckoutAsync(new CheckoutRequest(20m));

        Assert.True(checkoutResult.Success);
        var saleId = checkoutResult.Summary!.SaleId;

        // 9: inmediatamente después, consultar SalesHistoryService con filtro "Hoy" (día local
        // actual, igual conversión que SalesHistoryViewModel.ToStartOfDayUtc/ToExclusiveEndOfDayUtc).
        var salesHistoryQuery = new EfSalesHistoryQuery(context);
        var salesHistoryService = new SalesHistoryService(userSession, salesHistoryQuery);

        var today = DateTime.UtcNow.ToLocalTime().Date;
        var filter = new SalesHistoryFilter(FromUtc: ToStartOfDayUtc(today), ToUtc: ToExclusiveEndOfDayUtc(today));

        var page = await salesHistoryService.SearchPageAsync(filter, 0, 50);
        var summary = await salesHistoryService.GetSummaryAsync(filter);
        var detail = await salesHistoryService.GetDetailAsync(new SaleId(saleId));

        // 10: la venta recién completada debe aparecer.
        Assert.Contains(page.Items, item => item.SaleId.Value == saleId);
        Assert.True(summary.SalesCount >= 1);
        Assert.NotNull(detail);
        Assert.Equal(saleId, detail!.SaleId.Value);
        Assert.Equal(20m, detail.Total);
    }
}
