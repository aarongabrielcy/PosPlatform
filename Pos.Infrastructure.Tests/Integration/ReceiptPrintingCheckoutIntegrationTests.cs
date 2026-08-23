using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Authentication;
using Pos.Application.Receipts;
using Pos.Application.RegisterSessions;
using Pos.Application.Sales.Checkout;
using Pos.Application.SalesCart;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;
using Pos.Infrastructure.Authentication;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Persistence.Repositories;
using Pos.Infrastructure.RegisterSessions;
using Pos.Infrastructure.SalesCart;
using Pos.Infrastructure.Tests.Enforcement;
using Pos.Infrastructure.Tests.Persistence;
using Pos.Infrastructure.Time;

namespace Pos.Infrastructure.Tests.Integration;

// BASIC-PRN-01, secciones 46/47 de la tarea: la prueba MÁS CRÍTICA del ticket. Reproduce con SQLite
// real (no fakes de repositorio) que un fallo de impresora, ocurrido DESPUÉS de que CheckoutService
// ya confirmó su commit financiero, nunca deshace Sale/Payment/Inventory ni crea una segunda venta;
// y que un reimpreso posterior (exitoso o fallido) tampoco muta ningún estado de negocio.
public class ReceiptPrintingCheckoutIntegrationTests
{
    private static PosDbContext CreateContext(SqliteConnection connection)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PosDbContext>();
        optionsBuilder.UseSqlite(connection);

        return new PosDbContext(optionsBuilder.Options);
    }

    [Fact]
    public async Task PrinterFailureAfterSuccessfulCheckoutNeverAltersSaleOrPaymentOrInventoryOrCreatesASecondSale()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

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
            [Permission.ProcessSale, Permission.ViewSalesHistory, Permission.ReprintReceipt]));

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

        var checkoutService = new CheckoutService(
            userSession, registerSession, cart,
            new EfProductRepository(context), new EfInventoryItemRepository(context), new EfInventoryMovementRepository(context),
            new EfSaleRepository(context), new FakeInstallationEnforcementStateService(), context, new SystemClock());

        var checkoutResult = await checkoutService.CheckoutAsync(new CheckoutRequest(20m));

        Assert.True(checkoutResult.Success);
        var saleId = checkoutResult.Summary!.SaleId;

        // Instantánea del estado de negocio INMEDIATAMENTE después del commit financiero exitoso,
        // antes de intentar imprimir (sección 46: "Checkout financial transaction succeeds").
        var salesCountBefore = await context.Sales.CountAsync();
        var paymentsCountBefore = await context.Payments.CountAsync();
        var inventoryQuantityBefore = await context.InventoryItems.Where(i => i.ProductId == graph.ProductId).Select(i => i.Quantity).SingleAsync();
        var movementsCountBefore = await context.InventoryMovements.CountAsync();
        var registerSessionStatusBefore = await context.RegisterSessions.Where(rs => rs.Id == graph.RegisterSessionId).Select(rs => rs.Status).SingleAsync();
        var cashMovementsCountBefore = await context.CashMovements.CountAsync();

        // La impresora falla DESPUÉS del commit (sección 46: "printer fails -> sale STILL completed").
        var printer = new FakeReceiptPrinter { ResultStatus = PrinterOutcomeStatus.PrintFailed };
        var printingService = new ReceiptPrintingService(
            userSession,
            new EfSalesHistoryQuery(context),
            new EfOrganizationRepository(context),
            new FakeReceiptFormatter(),
            printer,
            new FixedReceiptPrinterOptionsProvider(new ReceiptPrinterOptions { Enabled = true, PrinterName = "TM-T20", AutoPrint = true }),
            new SystemClock());

        var printResult = await printingService.PrintAfterSaleAsync(saleId, cashTendered: 20m, changeDue: 0m);

        Assert.False(printResult.Success);
        Assert.Equal(ReceiptPrintResultStatus.PrintFailed, printResult.Status);
        Assert.Equal(1, printer.PrintCallCount);

        // El fallo de impresión NUNCA altera lo ya persistido (sección 46/22): mismos conteos,
        // misma cantidad de inventario, ninguna segunda Sale.
        Assert.Equal(salesCountBefore, await context.Sales.CountAsync());
        Assert.Equal(paymentsCountBefore, await context.Payments.CountAsync());
        Assert.Equal(inventoryQuantityBefore, await context.InventoryItems.Where(i => i.ProductId == graph.ProductId).Select(i => i.Quantity).SingleAsync());
        Assert.Equal(movementsCountBefore, await context.InventoryMovements.CountAsync());
        Assert.Equal(registerSessionStatusBefore, await context.RegisterSessions.Where(rs => rs.Id == graph.RegisterSessionId).Select(rs => rs.Status).SingleAsync());
        Assert.Equal(cashMovementsCountBefore, await context.CashMovements.CountAsync());
        Assert.Equal(1, await context.Sales.CountAsync(s => s.Id == saleId));

        var saleAfterFailedPrint = await context.Sales.SingleAsync(s => s.Id == saleId);
        Assert.Equal(Domain.Sales.SaleStatus.Completed, saleAfterFailedPrint.Status);

        // ---------- Reimpreso (sección 47): solo salida, ninguna mutación de negocio ----------

        var salesCountBeforeReprint = await context.Sales.CountAsync();
        var paymentsCountBeforeReprint = await context.Payments.CountAsync();
        var inventoryQuantityBeforeReprint = await context.InventoryItems.Where(i => i.ProductId == graph.ProductId).Select(i => i.Quantity).SingleAsync();
        var movementsCountBeforeReprint = await context.InventoryMovements.CountAsync();
        var cashMovementsCountBeforeReprint = await context.CashMovements.CountAsync();

        var succeedingPrinter = new FakeReceiptPrinter { ResultStatus = PrinterOutcomeStatus.Success };
        var reprintFormatter = new FakeReceiptFormatter();
        var reprintService = new ReceiptPrintingService(
            userSession,
            new EfSalesHistoryQuery(context),
            new EfOrganizationRepository(context),
            reprintFormatter,
            succeedingPrinter,
            new FixedReceiptPrinterOptionsProvider(new ReceiptPrinterOptions { Enabled = true, PrinterName = "TM-T20" }),
            new SystemClock());

        var reprintResult = await reprintService.ReprintAsync(saleId);

        Assert.True(reprintResult.Success);
        Assert.Equal(1, succeedingPrinter.PrintCallCount);
        Assert.True(reprintFormatter.LastReceipt!.IsReprint);
        Assert.Null(reprintFormatter.LastReceipt!.CashTendered);
        Assert.Null(reprintFormatter.LastReceipt!.ChangeDue);
        Assert.Equal(20m, reprintFormatter.LastReceipt!.Total);

        Assert.Equal(salesCountBeforeReprint, await context.Sales.CountAsync());
        Assert.Equal(paymentsCountBeforeReprint, await context.Payments.CountAsync());
        Assert.Equal(inventoryQuantityBeforeReprint, await context.InventoryItems.Where(i => i.ProductId == graph.ProductId).Select(i => i.Quantity).SingleAsync());
        Assert.Equal(movementsCountBeforeReprint, await context.InventoryMovements.CountAsync());
        Assert.Equal(cashMovementsCountBeforeReprint, await context.CashMovements.CountAsync());
    }

    [Fact]
    public async Task ReprintWithoutReprintReceiptPermissionIsDeniedAtServiceLevel()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context);
        var sale = await SqliteSeedHelper.SeedSaleAsync(
            context, graph.OrganizationId, graph.BranchId, graph.RegisterSessionId, graph.UserId, graph.ProductId,
            status: Domain.Sales.SaleStatus.Completed, completedAtUtc: SqliteSeedHelper.DefaultTimestamp);

        var userSession = new InMemoryCurrentUserSession();
        userSession.SetAuthenticatedUser(new AuthenticatedUser(
            new UserId(graph.UserId),
            new OrganizationId(graph.OrganizationId),
            new RoleId(graph.RoleId),
            "JPEREZ",
            "Juan Pérez",
            "Cajero",
            [Permission.ViewSalesHistory]));

        var printer = new FakeReceiptPrinter();
        var printingService = new ReceiptPrintingService(
            userSession,
            new EfSalesHistoryQuery(context),
            new EfOrganizationRepository(context),
            new FakeReceiptFormatter(),
            printer,
            new FixedReceiptPrinterOptionsProvider(new ReceiptPrinterOptions { Enabled = true, PrinterName = "TM-T20" }),
            new SystemClock());

        var result = await printingService.ReprintAsync(sale.Id);

        Assert.Equal(ReceiptPrintResultStatus.NotAuthorized, result.Status);
        Assert.Equal(0, printer.PrintCallCount);
    }
}
