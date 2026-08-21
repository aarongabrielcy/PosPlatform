using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Reports;
using Pos.Domain.CashMovements;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.RegisterSessions;
using Pos.Domain.Sales;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Persistence.Records;
using Pos.Infrastructure.Persistence.Repositories;
using Pos.Infrastructure.Tests.Persistence;

namespace Pos.Infrastructure.Tests.Integration;

// BASIC-RPT-01: valida los reportes operativos (EfOperationalReportsQuery) contra SQLite real
// (Foreign Keys=True), reproduciendo los escenarios numéricos congelados en la tarea (secciones
// 41-46): Sales Summary, Register Closure, Product Sales, Operator Activity y el filtro de fecha.
public class OperationalReportsSqliteIntegrationTests
{
    private static readonly DateTimeOffset Day = new(2026, 6, 1, 8, 0, 0, TimeSpan.Zero);

    private static PosDbContext CreateContext(SqliteConnection connection)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PosDbContext>();
        optionsBuilder.UseSqlite(connection);

        return new PosDbContext(optionsBuilder.Options);
    }

    // Sección 41: Cash 200 + Card 300 -> SaleCount 2, CashSales 200, CardSales 300, GrossSales 500.
    // Agregar CashIn 100 / CashOut 40 no debe alterar ninguno de los tres valores (sección 25).
    [Fact]
    public async Task SalesSummaryReflectsGrossCashAndCardSalesAndIgnoresCashMovements()
    {
        using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        connection.Open();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context, timestamp: Day);

        var cashSaleId = Guid.NewGuid();
        var cashSale = new SaleRecord
        {
            Id = cashSaleId, OrganizationId = graph.OrganizationId, BranchId = graph.BranchId,
            RegisterSessionId = graph.RegisterSessionId, CreatedByUserId = graph.UserId, Currency = "MXN",
            Status = SaleStatus.Completed, CreatedAtUtc = Day, CompletedAtUtc = Day,
        };
        cashSale.Lines.Add(new SaleLineRecord
        {
            Id = Guid.NewGuid(), SaleId = cashSaleId, ProductId = graph.ProductId, ProductSku = "SKU-001",
            ProductName = "Producto de prueba", Quantity = 1m, UnitPriceAmount = 200m, Currency = "MXN", Sale = cashSale,
        });
        cashSale.Payments.Add(new PaymentRecord
        {
            Id = Guid.NewGuid(), SaleId = cashSaleId, Method = PaymentMethod.Cash, Amount = 200m,
            Currency = "MXN", PaidAtUtc = Day, Sale = cashSale,
        });
        context.Add(cashSale);

        var cardSaleId = Guid.NewGuid();
        var cardSale = new SaleRecord
        {
            Id = cardSaleId, OrganizationId = graph.OrganizationId, BranchId = graph.BranchId,
            RegisterSessionId = graph.RegisterSessionId, CreatedByUserId = graph.UserId, Currency = "MXN",
            Status = SaleStatus.Completed, CreatedAtUtc = Day, CompletedAtUtc = Day,
        };
        cardSale.Lines.Add(new SaleLineRecord
        {
            Id = Guid.NewGuid(), SaleId = cardSaleId, ProductId = graph.ProductId, ProductSku = "SKU-001",
            ProductName = "Producto de prueba", Quantity = 1m, UnitPriceAmount = 300m, Currency = "MXN", Sale = cardSale,
        });
        cardSale.Payments.Add(new PaymentRecord
        {
            Id = Guid.NewGuid(), SaleId = cardSaleId, Method = PaymentMethod.Card, Amount = 300m,
            Currency = "MXN", PaidAtUtc = Day, Reference = "AUTH-1", Sale = cardSale,
        });
        context.Add(cardSale);

        context.Add(CashMovement(graph.RegisterSessionId, graph.UserId, CashMovementType.CashIn, 100m, "Fondo extra"));
        context.Add(CashMovement(graph.RegisterSessionId, graph.UserId, CashMovementType.CashOut, 40m, "Retiro parcial"));

        await context.CommitAsync(CancellationToken.None);

        var query = new EfOperationalReportsQuery(context);
        var summary = await query.GetSalesSummaryAsync(
            new OrganizationId(graph.OrganizationId), Day.AddHours(-1), Day.AddDays(1), CancellationToken.None);

        Assert.Equal(2, summary.SaleCount);
        Assert.Equal(200m, summary.CashSales);
        Assert.Equal(300m, summary.CardSales);
        Assert.Equal(500m, summary.GrossSales);
    }

    // Sección 42: OpeningFloat 500, CashSales 200, CardSales 300, CashIn 100, CashOut 40 ->
    // ExpectedCash 760. CountedCash 750 -> Difference -10. GrossSales 500.
    [Fact]
    public async Task RegisterClosureReportReturnsTheExactReconciliationNumbers()
    {
        using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        connection.Open();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context, timestamp: Day);

        // Ajusta la sesión sembrada (Open, OpeningFloat 100) a OpeningFloat 500 y Closed con el
        // desglose exacto de la tarea.
        var session = await context.Set<RegisterSessionRecord>().SingleAsync(s => s.Id == graph.RegisterSessionId);
        session.OpeningFloatAmount = 500m;
        session.Status = RegisterSessionStatus.Closed;
        session.ClosedByUserId = graph.UserId;
        session.ExpectedCashAmount = 760m;
        session.ExpectedCashCurrency = "MXN";
        session.CountedCashAmount = 750m;
        session.CountedCashCurrency = "MXN";
        session.CashDifferenceAmount = -10m;
        session.CashDifferenceCurrency = "MXN";
        session.ClosedAtUtc = Day.AddHours(9);

        var cashSaleId = Guid.NewGuid();
        var cashSale = new SaleRecord
        {
            Id = cashSaleId, OrganizationId = graph.OrganizationId, BranchId = graph.BranchId,
            RegisterSessionId = graph.RegisterSessionId, CreatedByUserId = graph.UserId, Currency = "MXN",
            Status = SaleStatus.Completed, CreatedAtUtc = Day, CompletedAtUtc = Day,
        };
        cashSale.Lines.Add(new SaleLineRecord
        {
            Id = Guid.NewGuid(), SaleId = cashSaleId, ProductId = graph.ProductId, ProductSku = "SKU-001",
            ProductName = "Producto de prueba", Quantity = 1m, UnitPriceAmount = 200m, Currency = "MXN", Sale = cashSale,
        });
        cashSale.Payments.Add(new PaymentRecord
        {
            Id = Guid.NewGuid(), SaleId = cashSaleId, Method = PaymentMethod.Cash, Amount = 200m,
            Currency = "MXN", PaidAtUtc = Day, Sale = cashSale,
        });
        context.Add(cashSale);

        var cardSaleId = Guid.NewGuid();
        var cardSale = new SaleRecord
        {
            Id = cardSaleId, OrganizationId = graph.OrganizationId, BranchId = graph.BranchId,
            RegisterSessionId = graph.RegisterSessionId, CreatedByUserId = graph.UserId, Currency = "MXN",
            Status = SaleStatus.Completed, CreatedAtUtc = Day, CompletedAtUtc = Day,
        };
        cardSale.Lines.Add(new SaleLineRecord
        {
            Id = Guid.NewGuid(), SaleId = cardSaleId, ProductId = graph.ProductId, ProductSku = "SKU-001",
            ProductName = "Producto de prueba", Quantity = 1m, UnitPriceAmount = 300m, Currency = "MXN", Sale = cardSale,
        });
        cardSale.Payments.Add(new PaymentRecord
        {
            Id = Guid.NewGuid(), SaleId = cardSaleId, Method = PaymentMethod.Card, Amount = 300m,
            Currency = "MXN", PaidAtUtc = Day, Reference = "AUTH-2", Sale = cardSale,
        });
        context.Add(cardSale);

        context.Add(CashMovement(graph.RegisterSessionId, graph.UserId, CashMovementType.CashIn, 100m, "Fondo extra"));
        context.Add(CashMovement(graph.RegisterSessionId, graph.UserId, CashMovementType.CashOut, 40m, "Retiro parcial"));

        await context.CommitAsync(CancellationToken.None);

        var query = new EfOperationalReportsQuery(context);
        var closures = await query.GetRegisterClosuresAsync(
            new OrganizationId(graph.OrganizationId), Day, Day.AddDays(1), CancellationToken.None);

        var closure = Assert.Single(closures);
        Assert.Equal(500m, closure.OpeningFloat);
        Assert.Equal(200m, closure.CashSales);
        Assert.Equal(300m, closure.CardSales);
        Assert.Equal(500m, closure.GrossSales);
        Assert.Equal(100m, closure.CashIn);
        Assert.Equal(40m, closure.CashOut);
        Assert.Equal(760m, closure.ExpectedCash);
        Assert.Equal(750m, closure.CountedCash);
        Assert.Equal(-10m, closure.Difference);

        var detail = await query.GetRegisterClosureDetailAsync(
            new OrganizationId(graph.OrganizationId), new RegisterSessionId(graph.RegisterSessionId), CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal(closure.ExpectedCash, detail!.ExpectedCash);
        Assert.Equal(closure.Difference, detail.Difference);
    }

    // Sección 43: Producto A 2 unidades @ 100 -> qty 2 / amount 200. Producto B 3 unidades @ 50 ->
    // qty 3 / amount 150.
    [Fact]
    public async Task ProductSalesReportAggregatesQuantityAndAmountPerProduct()
    {
        using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        connection.Open();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context, timestamp: Day, includeProduct: false);

        var productAId = Guid.NewGuid();
        var productBId = Guid.NewGuid();
        context.Add(new ProductRecord
        {
            Id = productAId, OrganizationId = graph.OrganizationId, Sku = "SKU-A", Name = "Producto A",
            SalePriceAmount = 100m, SalePriceCurrency = "MXN", TracksInventory = false, IsActive = true, CreatedAtUtc = Day,
        });
        context.Add(new ProductRecord
        {
            Id = productBId, OrganizationId = graph.OrganizationId, Sku = "SKU-B", Name = "Producto B",
            SalePriceAmount = 50m, SalePriceCurrency = "MXN", TracksInventory = false, IsActive = true, CreatedAtUtc = Day,
        });

        var saleId = Guid.NewGuid();
        var sale = new SaleRecord
        {
            Id = saleId, OrganizationId = graph.OrganizationId, BranchId = graph.BranchId,
            RegisterSessionId = graph.RegisterSessionId, CreatedByUserId = graph.UserId, Currency = "MXN",
            Status = SaleStatus.Completed, CreatedAtUtc = Day, CompletedAtUtc = Day,
        };
        sale.Lines.Add(new SaleLineRecord
        {
            Id = Guid.NewGuid(), SaleId = saleId, ProductId = productAId, ProductSku = "SKU-A",
            ProductName = "Producto A", Quantity = 2m, UnitPriceAmount = 100m, Currency = "MXN", Sale = sale,
        });
        sale.Lines.Add(new SaleLineRecord
        {
            Id = Guid.NewGuid(), SaleId = saleId, ProductId = productBId, ProductSku = "SKU-B",
            ProductName = "Producto B", Quantity = 3m, UnitPriceAmount = 50m, Currency = "MXN", Sale = sale,
        });
        sale.Payments.Add(new PaymentRecord
        {
            Id = Guid.NewGuid(), SaleId = saleId, Method = PaymentMethod.Cash, Amount = 350m,
            Currency = "MXN", PaidAtUtc = Day, Sale = sale,
        });
        context.Add(sale);

        await context.CommitAsync(CancellationToken.None);

        var query = new EfOperationalReportsQuery(context);
        var items = await query.GetProductSalesAsync(
            new OrganizationId(graph.OrganizationId), Day, Day.AddDays(1), CancellationToken.None);

        Assert.Equal(2, items.Count);
        var productA = Assert.Single(items, i => i.Sku == "SKU-A");
        Assert.Equal(2m, productA.QuantitySold);
        Assert.Equal(200m, productA.SalesAmount);

        var productB = Assert.Single(items, i => i.Sku == "SKU-B");
        Assert.Equal(3m, productB.QuantitySold);
        Assert.Equal(150m, productB.SalesAmount);
    }

    // Sección 45: Cajero A (2 ventas, $300) y Manager B (1 venta, $200) -> atribución y agregados
    // correctos por operador.
    [Fact]
    public async Task OperatorActivityReportAggregatesPerOperator()
    {
        using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        connection.Open();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context, timestamp: Day);

        var managerId = Guid.NewGuid();
        context.Add(new UserRecord
        {
            Id = managerId, OrganizationId = graph.OrganizationId, RoleId = graph.RoleId, Username = "MANAGERB",
            DisplayName = "Manager B", PasswordHash = SyntheticPasswordHash, IsActive = true, CreatedAtUtc = Day,
        });
        await context.CommitAsync(CancellationToken.None);

        void AddCashSale(Guid createdByUserId, decimal amount)
        {
            var id = Guid.NewGuid();
            var sale = new SaleRecord
            {
                Id = id, OrganizationId = graph.OrganizationId, BranchId = graph.BranchId,
                RegisterSessionId = graph.RegisterSessionId, CreatedByUserId = createdByUserId, Currency = "MXN",
                Status = SaleStatus.Completed, CreatedAtUtc = Day, CompletedAtUtc = Day,
            };
            sale.Lines.Add(new SaleLineRecord
            {
                Id = Guid.NewGuid(), SaleId = id, ProductId = graph.ProductId, ProductSku = "SKU-001",
                ProductName = "Producto de prueba", Quantity = 1m, UnitPriceAmount = amount, Currency = "MXN", Sale = sale,
            });
            sale.Payments.Add(new PaymentRecord
            {
                Id = Guid.NewGuid(), SaleId = id, Method = PaymentMethod.Cash, Amount = amount,
                Currency = "MXN", PaidAtUtc = Day, Sale = sale,
            });
            context.Add(sale);
        }

        AddCashSale(graph.UserId, 150m);
        AddCashSale(graph.UserId, 150m);
        AddCashSale(managerId, 200m);

        await context.CommitAsync(CancellationToken.None);

        var query = new EfOperationalReportsQuery(context);
        var items = await query.GetOperatorActivityAsync(
            new OrganizationId(graph.OrganizationId), Day, Day.AddDays(1), CancellationToken.None);

        Assert.Equal(2, items.Count);

        var cashierA = Assert.Single(items, i => i.UserId.Value == graph.UserId);
        Assert.Equal(2, cashierA.CompletedSaleCount);
        Assert.Equal(300m, cashierA.GrossSales);

        var managerB = Assert.Single(items, i => i.UserId.Value == managerId);
        Assert.Equal(1, managerB.CompletedSaleCount);
        Assert.Equal(200m, managerB.GrossSales);
    }

    // Sección 46: dentro del rango incluido, antes de From excluido, después de To excluido.
    [Fact]
    public async Task SalesSummaryExcludesSalesOutsideTheDateRange()
    {
        using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        connection.Open();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context, timestamp: Day);

        void AddCashSale(DateTimeOffset completedAtUtc, decimal amount)
        {
            var id = Guid.NewGuid();
            var sale = new SaleRecord
            {
                Id = id, OrganizationId = graph.OrganizationId, BranchId = graph.BranchId,
                RegisterSessionId = graph.RegisterSessionId, CreatedByUserId = graph.UserId, Currency = "MXN",
                Status = SaleStatus.Completed, CreatedAtUtc = completedAtUtc, CompletedAtUtc = completedAtUtc,
            };
            sale.Lines.Add(new SaleLineRecord
            {
                Id = Guid.NewGuid(), SaleId = id, ProductId = graph.ProductId, ProductSku = "SKU-001",
                ProductName = "Producto de prueba", Quantity = 1m, UnitPriceAmount = amount, Currency = "MXN", Sale = sale,
            });
            sale.Payments.Add(new PaymentRecord
            {
                Id = Guid.NewGuid(), SaleId = id, Method = PaymentMethod.Cash, Amount = amount,
                Currency = "MXN", PaidAtUtc = completedAtUtc, Sale = sale,
            });
            context.Add(sale);
        }

        var fromUtc = Day;
        var toUtcExclusive = Day.AddDays(1);

        AddCashSale(fromUtc.AddMinutes(-1), 10m); // antes de From: excluida
        AddCashSale(fromUtc, 20m); // exactamente From: incluida
        AddCashSale(toUtcExclusive.AddMinutes(-1), 30m); // dentro del rango: incluida
        AddCashSale(toUtcExclusive, 40m); // exactamente To (exclusivo): excluida

        await context.CommitAsync(CancellationToken.None);

        var query = new EfOperationalReportsQuery(context);
        var summary = await query.GetSalesSummaryAsync(
            new OrganizationId(graph.OrganizationId), fromUtc, toUtcExclusive, CancellationToken.None);

        Assert.Equal(2, summary.SaleCount);
        Assert.Equal(50m, summary.GrossSales);
    }

    private const string SyntheticPasswordHash = "v1$pbkdf2-sha256$210000$c2FsdC1zeW50aGV0aWM=$aGFzaC1zeW50aGV0aWM=";

    private static CashMovementRecord CashMovement(
        Guid registerSessionId, Guid actorUserId, CashMovementType type, decimal amount, string reason) =>
        new()
        {
            Id = Guid.NewGuid(),
            RegisterSessionId = registerSessionId,
            ActorUserId = actorUserId,
            Type = type,
            Amount = amount,
            Currency = "MXN",
            Reason = reason,
            CreatedAtUtc = Day,
        };
}
