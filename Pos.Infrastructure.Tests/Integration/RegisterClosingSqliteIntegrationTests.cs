using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Authentication;
using Pos.Application.RegisterSessions;
using Pos.Application.Sales.Checkout;
using Pos.Application.SalesCart;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.RegisterSessions;
using Pos.Domain.Sales;
using Pos.Domain.Security;
using Pos.Infrastructure.Authentication;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Persistence.Records;
using Pos.Infrastructure.Persistence.Repositories;
using Pos.Infrastructure.RegisterSessions;
using Pos.Infrastructure.SalesCart;
using Pos.Infrastructure.Time;

namespace Pos.Infrastructure.Tests.Integration;

// TAREA 25A-FIX sección 3: reproduce exactamente el escenario manual reportado por el usuario
// (defecto 1) usando los servicios reales de punta a punta -- RegisterSessionService.OpenAsync,
// tres cobros reales vía CheckoutService (nunca una Sale Completed insertada a mano),
// RegisterSessionService.GetClosingSummaryAsync como vista previa y RegisterSessionService.CloseAsync
// -- contra SQLite real (Foreign Keys=True), verificando en cada paso el estado persistido.
public class RegisterClosingSqliteIntegrationTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    // Hash sintético únicamente para satisfacer la invariante Domain; no es un hash PBKDF2 real y
    // no debe usarse para autenticación.
    private const string SyntheticPasswordHash = "v1$pbkdf2-sha256$210000$c2FsdC1zeW50aGV0aWM=$aGFzaC1zeW50aGV0aWM=";

    private static PosDbContext CreateContext(SqliteConnection connection)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PosDbContext>();
        optionsBuilder.UseSqlite(connection);

        return new PosDbContext(optionsBuilder.Options);
    }

    private static RegisterSessionService BuildRegisterSessionService(
        PosDbContext context, ICurrentUserSession currentUserSession, ICurrentRegisterSession currentRegisterSession) =>
        new(
            currentUserSession,
            currentRegisterSession,
            new EfOrganizationRepository(context),
            new EfBranchRepository(context),
            new EfRegisterRepository(context),
            new EfRegisterSessionRepository(context),
            new EfUserRepository(context),
            new EfSaleRepository(context),
            new Enforcement.FakeInstallationEnforcementStateService(),
            context,
            new SystemClock());

    private static async Task<(Guid OrganizationId, Guid BranchId, Guid RoleId, Guid UserId, Guid RegisterId, Guid ProductId)>
        SeedInstallationWithProductAsync(SqliteConnection connection)
    {
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organizationId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        context.Add(new OrganizationRecord
        {
            Id = organizationId, Name = "Acme Retail", IsActive = true, CreatedAtUtc = CreatedAtUtc,
        });
        context.Add(new BranchRecord
        {
            Id = branchId, OrganizationId = organizationId, Name = "Sucursal Centro", Code = "SUC-1",
            IsActive = true, CreatedAtUtc = CreatedAtUtc,
        });
        context.Add(new RegisterRecord
        {
            Id = registerId, BranchId = branchId, Name = "Caja 1", Code = "CAJA-1",
            IsActive = true, CreatedAtUtc = CreatedAtUtc,
        });

        var role = new RoleRecord
        {
            Id = roleId, OrganizationId = organizationId, Name = "Cajero", IsActive = true, CreatedAtUtc = CreatedAtUtc,
        };
        role.Permissions.Add(new RolePermissionRecord { RoleId = roleId, Permission = "OpenRegisterSession", Role = role });
        role.Permissions.Add(new RolePermissionRecord { RoleId = roleId, Permission = "CloseRegisterSession", Role = role });
        role.Permissions.Add(new RolePermissionRecord { RoleId = roleId, Permission = "ProcessSale", Role = role });
        context.Add(role);

        context.Add(new UserRecord
        {
            Id = userId, OrganizationId = organizationId, RoleId = roleId, Username = "CAJERO",
            DisplayName = "Cajero Uno", PasswordHash = SyntheticPasswordHash, IsActive = true, CreatedAtUtc = CreatedAtUtc,
        });

        context.Add(new ProductRecord
        {
            Id = productId, OrganizationId = organizationId, Sku = "SKU-001", Name = "Producto de prueba",
            SalePriceAmount = 10m, SalePriceCurrency = "MXN", TracksInventory = true, IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
        });

        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        return (organizationId, branchId, roleId, userId, registerId, productId);
    }

    [Fact]
    public async Task ClosingAfterThreeRealCashCheckoutsShowsAndPersistsOpeningFloatPlusCashSales()
    {
        using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        connection.Open();

        var (organizationId, branchId, roleId, userId, registerId, productId) =
            await SeedInstallationWithProductAsync(connection);

        await using (var context = CreateContext(connection))
        {
            context.Add(new InventoryItemRecord
            {
                Id = Guid.NewGuid(), BranchId = branchId, ProductId = productId, Quantity = 100m, ReorderPoint = 2m,
                CreatedAtUtc = CreatedAtUtc, UpdatedAtUtc = CreatedAtUtc,
            });

            await context.CommitAsync(CancellationToken.None);
        }

        var currentUserSession = new InMemoryCurrentUserSession();
        currentUserSession.SetAuthenticatedUser(new AuthenticatedUser(
            new UserId(userId), new OrganizationId(organizationId), new RoleId(roleId),
            "CAJERO", "Cajero Uno", "Cajero",
            [Permission.OpenRegisterSession, Permission.CloseRegisterSession, Permission.ProcessSale]));

        var currentRegisterSession = new InMemoryCurrentRegisterSession();

        // ---------- 1. Abrir caja con OpeningFloat = 500.00 vía RegisterSessionService.OpenAsync ----------

        RegisterSessionResult openResult;

        await using (var context = CreateContext(connection))
        {
            var service = BuildRegisterSessionService(context, currentUserSession, currentRegisterSession);
            openResult = await service.OpenAsync(new OpenRegisterSessionRequest(new RegisterId(registerId), 500m));
        }

        Assert.True(openResult.Success);
        var registerSessionId = currentRegisterSession.Current!.RegisterSessionId;

        // ---------- 2. Tres ventas reales en efectivo vía CheckoutService (nunca insertadas a mano) ----------

        decimal[] quantities = [2m, 3m, 4m]; // 20.00 + 30.00 + 40.00 = 90.00 en efectivo

        foreach (var quantity in quantities)
        {
            await using var context = CreateContext(connection);

            var cart = new InMemoryCurrentSalesCart();
            var lineTotal = quantity * 10m;
            cart.SetSnapshot(new SalesCartSnapshot(
                [new SalesCartLine(new ProductId(productId), "SKU-001", "Producto de prueba", quantity, 10m, lineTotal, "MXN", 100m, true)],
                "MXN"));

            var checkoutService = new CheckoutService(
                currentUserSession,
                currentRegisterSession,
                cart,
                new EfProductRepository(context),
                new EfInventoryItemRepository(context),
                new EfInventoryMovementRepository(context),
                new EfSaleRepository(context),
                new Enforcement.FakeInstallationEnforcementStateService(),
                context,
                new SystemClock());

            var checkoutResult = await checkoutService.CheckoutAsync(new CheckoutRequest(lineTotal));

            Assert.True(checkoutResult.Success);
        }

        // ---------- 3. Verificar que las 3 Sale queden Completed, Cash y de la sesión correcta ----------

        await using (var context = CreateContext(connection))
        {
            var sales = await context.Set<SaleRecord>()
                .Include(r => r.Payments)
                .Where(r => r.RegisterSessionId == registerSessionId.Value)
                .ToListAsync();

            Assert.Equal(3, sales.Count);
            Assert.All(sales, sale => Assert.Equal(SaleStatus.Completed, sale.Status));
            Assert.All(sales, sale => Assert.Equal(PaymentMethod.Cash, Assert.Single(sale.Payments).Method));
        }

        // ---------- 4. Vista previa: GetClosingSummaryAsync debe reflejar las ventas ANTES de cerrar ----------

        await using (var context = CreateContext(connection))
        {
            var service = BuildRegisterSessionService(context, currentUserSession, currentRegisterSession);
            var summaryResult = await service.GetClosingSummaryAsync();

            Assert.True(summaryResult.Success);
            Assert.Equal(500m, summaryResult.Summary!.OpeningFloat);
            Assert.Equal(90m, summaryResult.Summary.CompletedCashSales);
            Assert.Equal(590m, summaryResult.Summary.ExpectedCash);
        }

        // ---------- 5. Cerrar la caja y verificar ExpectedCash persistido ----------

        RegisterSessionResult closeResult;

        await using (var context = CreateContext(connection))
        {
            var service = BuildRegisterSessionService(context, currentUserSession, currentRegisterSession);
            closeResult = await service.CloseAsync(new CloseRegisterSessionRequest(590m));
        }

        Assert.True(closeResult.Success);
        Assert.Equal(590m, closeResult.Summary!.ExpectedAmount);
        Assert.Equal(0m, closeResult.Summary.Difference);

        await using (var context = CreateContext(connection))
        {
            var record = await context.Set<RegisterSessionRecord>().SingleAsync(r => r.Id == registerSessionId.Value);

            Assert.Equal(Pos.Domain.RegisterSessions.RegisterSessionStatus.Closed, record.Status);
            Assert.Equal(590m, record.ExpectedCashAmount);
            Assert.Equal(590m, record.CountedCashAmount);
            Assert.Equal(0m, record.CashDifferenceAmount);
        }
    }

    // TAREA 25C, invariante de cajón (sección 11 del encargo): una venta con Manual Card es una
    // venta pero NUNCA efectivo físico en el cajón. Reproduce el ejemplo del encargo: apertura 500,
    // ventas en efectivo 900, ventas con tarjeta 600 -> efectivo esperado 1400 (nunca 2000 = ventas
    // brutas). Igual que el test anterior, usa CheckoutService real de punta a punta, nunca una Sale
    // insertada a mano.
    [Fact]
    public async Task ClosingWithMixOfCashAndManualCardCheckoutsExcludesCardSalesFromExpectedCash()
    {
        using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        connection.Open();

        var (organizationId, branchId, roleId, userId, registerId, productId) =
            await SeedInstallationWithProductAsync(connection);

        await using (var context = CreateContext(connection))
        {
            context.Add(new InventoryItemRecord
            {
                Id = Guid.NewGuid(), BranchId = branchId, ProductId = productId, Quantity = 1000m, ReorderPoint = 2m,
                CreatedAtUtc = CreatedAtUtc, UpdatedAtUtc = CreatedAtUtc,
            });

            await context.CommitAsync(CancellationToken.None);
        }

        var currentUserSession = new InMemoryCurrentUserSession();
        currentUserSession.SetAuthenticatedUser(new AuthenticatedUser(
            new UserId(userId), new OrganizationId(organizationId), new RoleId(roleId),
            "CAJERO", "Cajero Uno", "Cajero",
            [Permission.OpenRegisterSession, Permission.CloseRegisterSession, Permission.ProcessSale]));

        var currentRegisterSession = new InMemoryCurrentRegisterSession();

        RegisterSessionResult openResult;

        await using (var context = CreateContext(connection))
        {
            var service = BuildRegisterSessionService(context, currentUserSession, currentRegisterSession);
            openResult = await service.OpenAsync(new OpenRegisterSessionRequest(new RegisterId(registerId), 500m));
        }

        Assert.True(openResult.Success);
        var registerSessionId = currentRegisterSession.Current!.RegisterSessionId;

        // Venta en efectivo de 900.00 (quantity 90 * 10.00).
        await using (var context = CreateContext(connection))
        {
            var cart = new InMemoryCurrentSalesCart();
            cart.SetSnapshot(new SalesCartSnapshot(
                [new SalesCartLine(new ProductId(productId), "SKU-001", "Producto de prueba", 90m, 10m, 900m, "MXN", 1000m, true)],
                "MXN"));

            var checkoutService = new CheckoutService(
                currentUserSession, currentRegisterSession, cart,
                new EfProductRepository(context), new EfInventoryItemRepository(context),
                new EfInventoryMovementRepository(context), new EfSaleRepository(context),
                new Enforcement.FakeInstallationEnforcementStateService(), context, new SystemClock());

            var checkoutResult = await checkoutService.CheckoutAsync(new CheckoutRequest(900m));

            Assert.True(checkoutResult.Success);
        }

        // Venta con Manual Card de 600.00 (quantity 60 * 10.00): terminal externa, referencia manual.
        await using (var context = CreateContext(connection))
        {
            var cart = new InMemoryCurrentSalesCart();
            cart.SetSnapshot(new SalesCartSnapshot(
                [new SalesCartLine(new ProductId(productId), "SKU-001", "Producto de prueba", 60m, 10m, 600m, "MXN", 910m, true)],
                "MXN"));

            var checkoutService = new CheckoutService(
                currentUserSession, currentRegisterSession, cart,
                new EfProductRepository(context), new EfInventoryItemRepository(context),
                new EfInventoryMovementRepository(context), new EfSaleRepository(context),
                new Enforcement.FakeInstallationEnforcementStateService(), context, new SystemClock());

            var checkoutResult = await checkoutService.CheckoutAsync(
                new CheckoutRequest(0m, CheckoutPaymentMethod.Card, "AUTH-9001"));

            Assert.True(checkoutResult.Success);
            Assert.Equal(CheckoutPaymentMethod.Card, checkoutResult.Summary!.PaymentMethod);
            Assert.Equal("AUTH-9001", checkoutResult.Summary.CardReference);
        }

        // ExpectedCash = OpeningFloat (500) + ventas en efectivo (900) = 1400. NUNCA 2000 (bruto).
        await using (var context = CreateContext(connection))
        {
            var service = BuildRegisterSessionService(context, currentUserSession, currentRegisterSession);
            var summaryResult = await service.GetClosingSummaryAsync();

            Assert.True(summaryResult.Success);
            Assert.Equal(500m, summaryResult.Summary!.OpeningFloat);
            Assert.Equal(900m, summaryResult.Summary.CompletedCashSales);
            Assert.Equal(600m, summaryResult.Summary.CompletedCardSales);
            Assert.Equal(1500m, summaryResult.Summary.GrossSales);
            Assert.Equal(1400m, summaryResult.Summary.ExpectedCash);
        }

        RegisterSessionResult closeResult;

        await using (var context = CreateContext(connection))
        {
            var service = BuildRegisterSessionService(context, currentUserSession, currentRegisterSession);
            closeResult = await service.CloseAsync(new CloseRegisterSessionRequest(1400m));
        }

        Assert.True(closeResult.Success);
        Assert.Equal(900m, closeResult.Summary!.CashSales);
        Assert.Equal(600m, closeResult.Summary.CardSales);
        Assert.Equal(1500m, closeResult.Summary.GrossSales);
        Assert.Equal(1400m, closeResult.Summary.ExpectedAmount);
        Assert.Equal(0m, closeResult.Summary.Difference);

        await using (var context = CreateContext(connection))
        {
            var record = await context.Set<RegisterSessionRecord>().SingleAsync(r => r.Id == registerSessionId.Value);

            Assert.Equal(Pos.Domain.RegisterSessions.RegisterSessionStatus.Closed, record.Status);
            Assert.Equal(1400m, record.ExpectedCashAmount);
            Assert.Equal(1400m, record.CountedCashAmount);
            Assert.Equal(0m, record.CashDifferenceAmount);
        }
    }

    // TAREA 25C-FIX sección 10/12: GrossSales debe seguir contando una venta pagada por un método
    // de dominio que ya existe (BankTransfer) aunque Checkout no lo exponga y aunque no tenga
    // desglose propio (Cash/Card). BankTransfer no es alcanzable desde Checkout, así que la venta se
    // inserta directamente (igual patrón que otras pruebas de infraestructura con datos históricos/
    // fuera del alcance de UI), nunca vía CheckoutService. Si GrossSales se derivara de
    // CashSales + CardSales, esta venta desaparecería silenciosamente del total.
    [Fact]
    public async Task ClosingSummaryIncludesACompletedBankTransferSaleInGrossSalesButNotInCashOrCardBreakdown()
    {
        using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        connection.Open();

        var (organizationId, branchId, roleId, userId, registerId, productId) =
            await SeedInstallationWithProductAsync(connection);

        var currentUserSession = new InMemoryCurrentUserSession();
        currentUserSession.SetAuthenticatedUser(new AuthenticatedUser(
            new UserId(userId), new OrganizationId(organizationId), new RoleId(roleId),
            "CAJERO", "Cajero Uno", "Cajero",
            [Permission.OpenRegisterSession, Permission.CloseRegisterSession, Permission.ProcessSale]));

        var currentRegisterSession = new InMemoryCurrentRegisterSession();

        RegisterSessionResult openResult;

        await using (var context = CreateContext(connection))
        {
            var service = BuildRegisterSessionService(context, currentUserSession, currentRegisterSession);
            openResult = await service.OpenAsync(new OpenRegisterSessionRequest(new RegisterId(registerId), 500m));
        }

        Assert.True(openResult.Success);
        var registerSessionId = currentRegisterSession.Current!.RegisterSessionId;

        // Venta en efectivo real de 900.00 vía CheckoutService, igual que en los demás tests.
        await using (var context = CreateContext(connection))
        {
            context.Add(new InventoryItemRecord
            {
                Id = Guid.NewGuid(), BranchId = branchId, ProductId = productId, Quantity = 1000m, ReorderPoint = 2m,
                CreatedAtUtc = CreatedAtUtc, UpdatedAtUtc = CreatedAtUtc,
            });
            await context.CommitAsync(CancellationToken.None);

            var cart = new InMemoryCurrentSalesCart();
            cart.SetSnapshot(new SalesCartSnapshot(
                [new SalesCartLine(new ProductId(productId), "SKU-001", "Producto de prueba", 90m, 10m, 900m, "MXN", 1000m, true)],
                "MXN"));

            var checkoutService = new CheckoutService(
                currentUserSession, currentRegisterSession, cart,
                new EfProductRepository(context), new EfInventoryItemRepository(context),
                new EfInventoryMovementRepository(context), new EfSaleRepository(context),
                new Enforcement.FakeInstallationEnforcementStateService(), context, new SystemClock());

            var checkoutResult = await checkoutService.CheckoutAsync(new CheckoutRequest(900m));

            Assert.True(checkoutResult.Success);
        }

        // Venta con BankTransfer de 200.00: no alcanzable desde Checkout en BASIC V1, se inserta
        // directamente como dato ya persistido (mismo patrón que otras pruebas de infraestructura).
        var bankTransferSaleId = Guid.NewGuid();

        await using (var context = CreateContext(connection))
        {
            var record = new SaleRecord
            {
                Id = bankTransferSaleId,
                OrganizationId = organizationId,
                BranchId = branchId,
                RegisterSessionId = registerSessionId.Value,
                CreatedByUserId = userId,
                Currency = "MXN",
                Status = SaleStatus.Completed,
                CreatedAtUtc = CreatedAtUtc,
                CompletedAtUtc = CreatedAtUtc,
            };
            record.Lines.Add(new SaleLineRecord
            {
                Id = Guid.NewGuid(), SaleId = bankTransferSaleId, ProductId = productId, ProductSku = "SKU-001",
                ProductName = "Producto de prueba", Quantity = 20m, UnitPriceAmount = 10m, Currency = "MXN", Sale = record,
            });
            record.Payments.Add(new PaymentRecord
            {
                Id = Guid.NewGuid(), SaleId = bankTransferSaleId, Method = PaymentMethod.BankTransfer, Amount = 200m,
                Currency = "MXN", PaidAtUtc = CreatedAtUtc, Reference = null, Sale = record,
            });

            context.Add(record);
            await context.CommitAsync(CancellationToken.None);
        }

        // GrossSales = 900 (Cash) + 200 (BankTransfer) = 1100, NUNCA CashSales + CardSales (= 900).
        // ExpectedCash sigue siendo solo Opening + Cash = 1400: BankTransfer tampoco es efectivo físico.
        await using (var context = CreateContext(connection))
        {
            var service = BuildRegisterSessionService(context, currentUserSession, currentRegisterSession);
            var summaryResult = await service.GetClosingSummaryAsync();

            Assert.True(summaryResult.Success);
            Assert.Equal(900m, summaryResult.Summary!.CompletedCashSales);
            Assert.Equal(0m, summaryResult.Summary.CompletedCardSales);
            Assert.Equal(1100m, summaryResult.Summary.GrossSales);
            Assert.Equal(1400m, summaryResult.Summary.ExpectedCash);
        }
    }
}
