using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Sales.History;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Sales;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Persistence.Records;
using Pos.Infrastructure.Persistence.Repositories;

namespace Pos.Infrastructure.Tests.Persistence.Repositories;

// EfSalesHistoryQuery contra SQLite real (TAREA 25B, sección 38-43): Organization A/Branch
// A/Register A/Cajero A, Organization B para probar aislamiento de tenant, varias Sales Completed
// con fechas/cajeros/registers/productos/cantidades/métodos de pago distintos.
public class EfSalesHistoryQueryTests
{
    private static PosDbContext CreateContext(SqliteConnection connection)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PosDbContext>();
        optionsBuilder.UseSqlite(connection);

        return new PosDbContext(optionsBuilder.Options);
    }

    // Inserta una Sale Completed con líneas/pagos arbitrarios: SqliteSeedHelper.SeedSaleAsync solo
    // soporta una línea y un pago fijos (Cash), insuficiente para cantidades >1, múltiples métodos
    // de pago o distintos productos/snapshots por venta (TAREA 25B, sección 38).
    private static async Task<Guid> SeedCompletedSaleAsync(
        PosDbContext context,
        Guid organizationId,
        Guid branchId,
        Guid registerSessionId,
        Guid createdByUserId,
        DateTimeOffset completedAtUtc,
        IReadOnlyList<(Guid ProductId, string Sku, string Name, decimal Quantity, decimal UnitPrice)> lines,
        IReadOnlyList<(PaymentMethod Method, decimal Amount)> payments,
        Guid? saleId = null,
        DateTimeOffset? createdAtUtc = null)
    {
        var id = saleId ?? Guid.NewGuid();

        var record = new SaleRecord
        {
            Id = id,
            OrganizationId = organizationId,
            BranchId = branchId,
            RegisterSessionId = registerSessionId,
            CreatedByUserId = createdByUserId,
            Currency = "MXN",
            Status = SaleStatus.Completed,
            CreatedAtUtc = createdAtUtc ?? completedAtUtc,
            CompletedAtUtc = completedAtUtc,
        };

        foreach (var line in lines)
        {
            record.Lines.Add(new SaleLineRecord
            {
                Id = Guid.NewGuid(),
                SaleId = id,
                ProductId = line.ProductId,
                ProductSku = line.Sku,
                ProductName = line.Name,
                Quantity = line.Quantity,
                UnitPriceAmount = line.UnitPrice,
                Currency = "MXN",
                Sale = record,
            });
        }

        foreach (var payment in payments)
        {
            record.Payments.Add(new PaymentRecord
            {
                Id = Guid.NewGuid(),
                SaleId = id,
                Method = payment.Method,
                Amount = payment.Amount,
                Currency = "MXN",
                PaidAtUtc = completedAtUtc,
                Sale = record,
            });
        }

        context.Add(record);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        return id;
    }

    private static async Task<Guid> SeedDraftSaleAsync(
        PosDbContext context, Guid organizationId, Guid branchId, Guid registerSessionId, Guid createdByUserId, Guid productId)
    {
        var id = Guid.NewGuid();

        var record = new SaleRecord
        {
            Id = id,
            OrganizationId = organizationId,
            BranchId = branchId,
            RegisterSessionId = registerSessionId,
            CreatedByUserId = createdByUserId,
            Currency = "MXN",
            Status = SaleStatus.Draft,
            CreatedAtUtc = SqliteSeedHelper.DefaultTimestamp,
            CompletedAtUtc = null,
        };

        record.Lines.Add(new SaleLineRecord
        {
            Id = Guid.NewGuid(),
            SaleId = id,
            ProductId = productId,
            ProductSku = "SKU-001",
            ProductName = "Producto de prueba",
            Quantity = 1m,
            UnitPriceAmount = 10m,
            Currency = "MXN",
            Sale = record,
        });

        context.Add(record);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        return id;
    }

    // ---------- Listado: alcance / estado ----------

    [Fact]
    public async Task SearchPageAsyncOnlyReturnsCompletedSalesForTheGivenOrganization()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graphA = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context, organizationId: Guid.NewGuid());
        var graphB = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context, organizationId: Guid.NewGuid());

        var completedSaleA = await SeedCompletedSaleAsync(
            context, graphA.OrganizationId, graphA.BranchId, graphA.RegisterSessionId, graphA.UserId,
            SqliteSeedHelper.DefaultTimestamp,
            [(graphA.ProductId, "SKU-001", "Producto A", 1m, 10m)],
            [(PaymentMethod.Cash, 10m)]);

        await SeedDraftSaleAsync(context, graphA.OrganizationId, graphA.BranchId, graphA.RegisterSessionId, graphA.UserId, graphA.ProductId);

        await SeedCompletedSaleAsync(
            context, graphB.OrganizationId, graphB.BranchId, graphB.RegisterSessionId, graphB.UserId,
            SqliteSeedHelper.DefaultTimestamp,
            [(graphB.ProductId, "SKU-001", "Producto B", 1m, 10m)],
            [(PaymentMethod.Cash, 10m)]);

        var query = new EfSalesHistoryQuery(context);

        var result = await query.SearchPageAsync(
            new OrganizationId(graphA.OrganizationId), SalesHistoryFilter.Empty, 0, 50, CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(completedSaleA, item.SaleId.Value);
    }

    [Fact]
    public async Task SearchPageAsyncOrdersByCompletedAtDescendingThenIdDescending()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context);

        var earlySale = await SeedCompletedSaleAsync(
            context, graph.OrganizationId, graph.BranchId, graph.RegisterSessionId, graph.UserId,
            SqliteSeedHelper.DefaultTimestamp,
            [(graph.ProductId, "SKU-001", "Producto", 1m, 10m)], [(PaymentMethod.Cash, 10m)]);

        var lateSale = await SeedCompletedSaleAsync(
            context, graph.OrganizationId, graph.BranchId, graph.RegisterSessionId, graph.UserId,
            SqliteSeedHelper.DefaultTimestamp.AddHours(2),
            [(graph.ProductId, "SKU-001", "Producto", 1m, 10m)], [(PaymentMethod.Cash, 10m)]);

        var query = new EfSalesHistoryQuery(context);

        var result = await query.SearchPageAsync(
            new OrganizationId(graph.OrganizationId), SalesHistoryFilter.Empty, 0, 50, CancellationToken.None);

        Assert.Equal([lateSale, earlySale], result.Items.Select(i => i.SaleId.Value));
    }

    // [fromInclusive, toExclusive): TAREA 25B, sección 12 — evita excluir ventas del resto del día
    // "Hasta".
    [Fact]
    public async Task DateFilterIsInclusiveOfFromAndExclusiveOfTo()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context);
        var dayStart = new DateTimeOffset(2026, 8, 4, 0, 0, 0, TimeSpan.Zero);
        var dayEnd = dayStart.AddDays(1);

        var atStart = await SeedCompletedSaleAsync(
            context, graph.OrganizationId, graph.BranchId, graph.RegisterSessionId, graph.UserId, dayStart,
            [(graph.ProductId, "SKU-001", "Producto", 1m, 10m)], [(PaymentMethod.Cash, 10m)]);

        var justBeforeEnd = await SeedCompletedSaleAsync(
            context, graph.OrganizationId, graph.BranchId, graph.RegisterSessionId, graph.UserId, dayEnd.AddSeconds(-1),
            [(graph.ProductId, "SKU-001", "Producto", 1m, 10m)], [(PaymentMethod.Cash, 10m)]);

        await SeedCompletedSaleAsync(
            context, graph.OrganizationId, graph.BranchId, graph.RegisterSessionId, graph.UserId, dayEnd,
            [(graph.ProductId, "SKU-001", "Producto", 1m, 10m)], [(PaymentMethod.Cash, 10m)]);

        var query = new EfSalesHistoryQuery(context);

        var result = await query.SearchPageAsync(
            new OrganizationId(graph.OrganizationId),
            new SalesHistoryFilter(FromUtc: dayStart, ToUtc: dayEnd),
            0, 50, CancellationToken.None);

        Assert.Equal(
            new[] { justBeforeEnd, atStart }.OrderByDescending(x => x).ToArray(),
            result.Items.Select(i => i.SaleId.Value).OrderByDescending(x => x).ToArray());
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task CashierFilterOnlyMatchesSalesCreatedByThatUser()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context);
        var otherUserId = Guid.NewGuid();
        context.Add(new UserRecord
        {
            Id = otherUserId,
            OrganizationId = graph.OrganizationId,
            RoleId = graph.RoleId,
            Username = "MLOPEZ",
            DisplayName = "María López",
            PasswordHash = "v1$pbkdf2-sha256$210000$c2FsdA==$aGFzaA==",
            IsActive = true,
            CreatedAtUtc = SqliteSeedHelper.DefaultTimestamp,
        });
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var saleByGraphUser = await SeedCompletedSaleAsync(
            context, graph.OrganizationId, graph.BranchId, graph.RegisterSessionId, graph.UserId,
            SqliteSeedHelper.DefaultTimestamp,
            [(graph.ProductId, "SKU-001", "Producto", 1m, 10m)], [(PaymentMethod.Cash, 10m)]);

        await SeedCompletedSaleAsync(
            context, graph.OrganizationId, graph.BranchId, graph.RegisterSessionId, otherUserId,
            SqliteSeedHelper.DefaultTimestamp,
            [(graph.ProductId, "SKU-001", "Producto", 1m, 10m)], [(PaymentMethod.Cash, 10m)]);

        var query = new EfSalesHistoryQuery(context);

        var result = await query.SearchPageAsync(
            new OrganizationId(graph.OrganizationId),
            new SalesHistoryFilter(CashierUserId: new UserId(graph.UserId)),
            0, 50, CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(saleByGraphUser, item.SaleId.Value);
    }

    [Fact]
    public async Task RegisterFilterOnlyMatchesSalesFromThatRegister()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context);

        var otherRegisterId = Guid.NewGuid();
        var otherRegisterSessionId = Guid.NewGuid();
        context.Add(new RegisterRecord
        {
            Id = otherRegisterId,
            BranchId = graph.BranchId,
            Name = "Caja 2",
            Code = "CAJA-2",
            IsActive = true,
            CreatedAtUtc = SqliteSeedHelper.DefaultTimestamp,
        });
        context.Add(new RegisterSessionRecord
        {
            Id = otherRegisterSessionId,
            RegisterId = otherRegisterId,
            OpenedByUserId = graph.UserId,
            OpeningFloatAmount = 100m,
            OpeningFloatCurrency = "MXN",
            Status = Pos.Domain.RegisterSessions.RegisterSessionStatus.Open,
            OpenedAtUtc = SqliteSeedHelper.DefaultTimestamp,
        });
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var saleAtRegister1 = await SeedCompletedSaleAsync(
            context, graph.OrganizationId, graph.BranchId, graph.RegisterSessionId, graph.UserId,
            SqliteSeedHelper.DefaultTimestamp,
            [(graph.ProductId, "SKU-001", "Producto", 1m, 10m)], [(PaymentMethod.Cash, 10m)]);

        await SeedCompletedSaleAsync(
            context, graph.OrganizationId, graph.BranchId, otherRegisterSessionId, graph.UserId,
            SqliteSeedHelper.DefaultTimestamp,
            [(graph.ProductId, "SKU-001", "Producto", 1m, 10m)], [(PaymentMethod.Cash, 10m)]);

        var query = new EfSalesHistoryQuery(context);

        var result = await query.SearchPageAsync(
            new OrganizationId(graph.OrganizationId),
            new SalesHistoryFilter(RegisterId: new RegisterId(graph.RegisterId)),
            0, 50, CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(saleAtRegister1, item.SaleId.Value);
    }

    // Sale con múltiples Payments (TAREA 25B, sección 18): coincide con el filtro si contiene AL
    // MENOS UN Payment de ese método, sin asumir 1:1.
    [Fact]
    public async Task PaymentMethodFilterMatchesSalesWithAtLeastOneMatchingPayment()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context);

        var mixedPaymentSale = await SeedCompletedSaleAsync(
            context, graph.OrganizationId, graph.BranchId, graph.RegisterSessionId, graph.UserId,
            SqliteSeedHelper.DefaultTimestamp,
            [(graph.ProductId, "SKU-001", "Producto", 1m, 30m)],
            [(PaymentMethod.Cash, 10m), (PaymentMethod.Card, 20m)]);

        var cashOnlySale = await SeedCompletedSaleAsync(
            context, graph.OrganizationId, graph.BranchId, graph.RegisterSessionId, graph.UserId,
            SqliteSeedHelper.DefaultTimestamp,
            [(graph.ProductId, "SKU-001", "Producto", 1m, 10m)], [(PaymentMethod.Cash, 10m)]);

        var query = new EfSalesHistoryQuery(context);

        var cashResult = await query.SearchPageAsync(
            new OrganizationId(graph.OrganizationId),
            new SalesHistoryFilter(PaymentMethod: PaymentMethod.Cash),
            0, 50, CancellationToken.None);
        var cardResult = await query.SearchPageAsync(
            new OrganizationId(graph.OrganizationId),
            new SalesHistoryFilter(PaymentMethod: PaymentMethod.Card),
            0, 50, CancellationToken.None);

        Assert.Equal(2, cashResult.Items.Count);
        Assert.Contains(cashResult.Items, i => i.SaleId.Value == mixedPaymentSale);
        Assert.Contains(cashResult.Items, i => i.SaleId.Value == cashOnlySale);

        var cardItem = Assert.Single(cardResult.Items);
        Assert.Equal(mixedPaymentSale, cardItem.SaleId.Value);
    }

    [Fact]
    public async Task SearchTermMatchesBySkuOrProductSnapshotNameOrExactSaleId()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context);

        var waterSale = await SeedCompletedSaleAsync(
            context, graph.OrganizationId, graph.BranchId, graph.RegisterSessionId, graph.UserId,
            SqliteSeedHelper.DefaultTimestamp,
            [(graph.ProductId, "AGUA-1L", "Agua 1L", 1m, 20m)], [(PaymentMethod.Cash, 20m)]);

        var sodaSale = await SeedCompletedSaleAsync(
            context, graph.OrganizationId, graph.BranchId, graph.RegisterSessionId, graph.UserId,
            SqliteSeedHelper.DefaultTimestamp,
            [(graph.ProductId, "REFR-001", "Refresco 600ml", 1m, 18m)], [(PaymentMethod.Cash, 18m)]);

        var query = new EfSalesHistoryQuery(context);
        var organizationId = new OrganizationId(graph.OrganizationId);

        var bySku = await query.SearchPageAsync(
            organizationId, new SalesHistoryFilter(SearchTerm: "AGUA-1L"), 0, 50, CancellationToken.None);
        var byName = await query.SearchPageAsync(
            organizationId, new SalesHistoryFilter(SearchTerm: "Refresco"), 0, 50, CancellationToken.None);
        var bySaleId = await query.SearchPageAsync(
            organizationId, new SalesHistoryFilter(SearchTerm: waterSale.ToString()), 0, 50, CancellationToken.None);

        Assert.Equal(waterSale, Assert.Single(bySku.Items).SaleId.Value);
        Assert.Equal(sodaSale, Assert.Single(byName.Items).SaleId.Value);
        Assert.Equal(waterSale, Assert.Single(bySaleId.Items).SaleId.Value);
    }

    [Fact]
    public async Task CombiningFiltersAppliesAllConditionsTogether()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context);

        var matching = await SeedCompletedSaleAsync(
            context, graph.OrganizationId, graph.BranchId, graph.RegisterSessionId, graph.UserId,
            SqliteSeedHelper.DefaultTimestamp,
            [(graph.ProductId, "AGUA-1L", "Agua 1L", 1m, 20m)], [(PaymentMethod.Cash, 20m)]);

        // Mismo cajero/caja/fecha pero método distinto: no debe calzar con el filtro por Card.
        await SeedCompletedSaleAsync(
            context, graph.OrganizationId, graph.BranchId, graph.RegisterSessionId, graph.UserId,
            SqliteSeedHelper.DefaultTimestamp,
            [(graph.ProductId, "AGUA-1L", "Agua 1L", 1m, 20m)], [(PaymentMethod.Card, 20m)]);

        var query = new EfSalesHistoryQuery(context);

        var result = await query.SearchPageAsync(
            new OrganizationId(graph.OrganizationId),
            new SalesHistoryFilter(
                FromUtc: SqliteSeedHelper.DefaultTimestamp,
                ToUtc: SqliteSeedHelper.DefaultTimestamp.AddDays(1),
                CashierUserId: new UserId(graph.UserId),
                RegisterId: new RegisterId(graph.RegisterId),
                PaymentMethod: PaymentMethod.Cash,
                SearchTerm: "AGUA-1L"),
            0, 50, CancellationToken.None);

        Assert.Equal(matching, Assert.Single(result.Items).SaleId.Value);
    }

    [Fact]
    public async Task SearchPageAsyncReturnsEmptyResultWhenNoSalesMatch()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context);

        var query = new EfSalesHistoryQuery(context);

        var result = await query.SearchPageAsync(
            new OrganizationId(graph.OrganizationId), SalesHistoryFilter.Empty, 0, 50, CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.False(result.HasNextPage);
    }

    // ItemCount = SUM(Quantity), no COUNT(Lines) (TAREA 25B, sección 19).
    [Fact]
    public async Task ItemCountIsTheSumOfLineQuantitiesNotTheNumberOfLines()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context);
        var secondProductId = Guid.NewGuid();
        context.Add(new ProductRecord
        {
            Id = secondProductId,
            OrganizationId = graph.OrganizationId,
            Sku = "SKU-002",
            Name = "Segundo producto",
            SalePriceAmount = 5m,
            SalePriceCurrency = "MXN",
            TracksInventory = true,
            IsActive = true,
            CreatedAtUtc = SqliteSeedHelper.DefaultTimestamp,
        });
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        await SeedCompletedSaleAsync(
            context, graph.OrganizationId, graph.BranchId, graph.RegisterSessionId, graph.UserId,
            SqliteSeedHelper.DefaultTimestamp,
            [
                (graph.ProductId, "SKU-001", "Producto uno", 3m, 10m),
                (secondProductId, "SKU-002", "Segundo producto", 5m, 5m),
            ],
            [(PaymentMethod.Cash, 55m)]);

        var query = new EfSalesHistoryQuery(context);

        var result = await query.SearchPageAsync(
            new OrganizationId(graph.OrganizationId), SalesHistoryFilter.Empty, 0, 50, CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(8m, item.ItemCount);
        Assert.Equal(55m, item.Total);
    }

    [Fact]
    public async Task PaymentSummaryAggregatesAmountsPerMethod()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context);

        await SeedCompletedSaleAsync(
            context, graph.OrganizationId, graph.BranchId, graph.RegisterSessionId, graph.UserId,
            SqliteSeedHelper.DefaultTimestamp,
            [(graph.ProductId, "SKU-001", "Producto", 1m, 30m)],
            [(PaymentMethod.Cash, 10m), (PaymentMethod.Cash, 5m), (PaymentMethod.Card, 15m)]);

        var query = new EfSalesHistoryQuery(context);

        var result = await query.SearchPageAsync(
            new OrganizationId(graph.OrganizationId), SalesHistoryFilter.Empty, 0, 50, CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(2, item.PaymentSummary.Count);
        Assert.Equal(15m, item.PaymentSummary.Single(p => p.Method == PaymentMethod.Cash).Amount);
        Assert.Equal(15m, item.PaymentSummary.Single(p => p.Method == PaymentMethod.Card).Amount);
    }

    // ---------- Paginación ----------

    [Fact]
    public async Task SearchPageAsyncPaginatesWithoutDuplicatesOrLossesAcrossPages()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context);
        var saleIds = new List<Guid>();

        for (var i = 0; i < 55; i++)
        {
            var id = await SeedCompletedSaleAsync(
                context, graph.OrganizationId, graph.BranchId, graph.RegisterSessionId, graph.UserId,
                SqliteSeedHelper.DefaultTimestamp.AddMinutes(i),
                [(graph.ProductId, "SKU-001", "Producto", 1m, 10m)], [(PaymentMethod.Cash, 10m)]);
            saleIds.Add(id);
        }

        var query = new EfSalesHistoryQuery(context);
        const int pageSize = 50;

        var page1 = await query.SearchPageAsync(
            new OrganizationId(graph.OrganizationId), SalesHistoryFilter.Empty, 0, pageSize, CancellationToken.None);
        var page2 = await query.SearchPageAsync(
            new OrganizationId(graph.OrganizationId), SalesHistoryFilter.Empty, pageSize, pageSize, CancellationToken.None);

        Assert.Equal(50, page1.Items.Count);
        Assert.True(page1.HasNextPage);
        Assert.Equal(5, page2.Items.Count);
        Assert.False(page2.HasNextPage);

        var combinedIds = page1.Items.Concat(page2.Items).Select(i => i.SaleId.Value).ToList();
        Assert.Equal(55, combinedIds.Distinct().Count());
        Assert.Equal(saleIds.OrderBy(x => x), combinedIds.OrderBy(x => x));
    }

    // ---------- Resumen (Summary) ----------

    [Fact]
    public async Task GetSummaryAsyncCountsAndTotalsOnlyTheFilteredSalesNotAPage()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context);

        await SeedCompletedSaleAsync(
            context, graph.OrganizationId, graph.BranchId, graph.RegisterSessionId, graph.UserId,
            SqliteSeedHelper.DefaultTimestamp,
            [(graph.ProductId, "SKU-001", "Producto", 1m, 100m)], [(PaymentMethod.Cash, 100m)]);

        await SeedCompletedSaleAsync(
            context, graph.OrganizationId, graph.BranchId, graph.RegisterSessionId, graph.UserId,
            SqliteSeedHelper.DefaultTimestamp,
            [(graph.ProductId, "SKU-001", "Producto", 1m, 200m)], [(PaymentMethod.Card, 200m)]);

        // Fuera del filtro de fecha: no debe contarse ni sumarse.
        await SeedCompletedSaleAsync(
            context, graph.OrganizationId, graph.BranchId, graph.RegisterSessionId, graph.UserId,
            SqliteSeedHelper.DefaultTimestamp.AddDays(5),
            [(graph.ProductId, "SKU-001", "Producto", 1m, 500m)], [(PaymentMethod.Cash, 500m)]);

        var query = new EfSalesHistoryQuery(context);

        var summary = await query.GetSummaryAsync(
            new OrganizationId(graph.OrganizationId),
            new SalesHistoryFilter(FromUtc: SqliteSeedHelper.DefaultTimestamp, ToUtc: SqliteSeedHelper.DefaultTimestamp.AddDays(1)),
            CancellationToken.None);

        Assert.Equal(2, summary.SalesCount);
        Assert.Equal(300m, summary.Total);
        Assert.Equal(100m, summary.PaymentBreakdown.Single(p => p.Method == PaymentMethod.Cash).Amount);
        Assert.Equal(200m, summary.PaymentBreakdown.Single(p => p.Method == PaymentMethod.Card).Amount);
    }

    [Fact]
    public async Task GetSummaryAsyncReturnsEmptyWhenNoSalesMatch()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context);

        var query = new EfSalesHistoryQuery(context);

        var summary = await query.GetSummaryAsync(
            new OrganizationId(graph.OrganizationId), SalesHistoryFilter.Empty, CancellationToken.None);

        Assert.Equal(0, summary.SalesCount);
        Assert.Equal(0m, summary.Total);
        Assert.Empty(summary.PaymentBreakdown);
    }

    // ---------- Detalle ----------

    [Fact]
    public async Task GetDetailAsyncReturnsTheExactSaleWithLinesPaymentsCashierAndRegister()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context);

        var saleId = await SeedCompletedSaleAsync(
            context, graph.OrganizationId, graph.BranchId, graph.RegisterSessionId, graph.UserId,
            SqliteSeedHelper.DefaultTimestamp,
            [(graph.ProductId, "SKU-001", "Producto de prueba", 3m, 10m)],
            [(PaymentMethod.Cash, 20m), (PaymentMethod.Card, 10m)]);

        var query = new EfSalesHistoryQuery(context);

        var detail = await query.GetDetailAsync(
            new OrganizationId(graph.OrganizationId), new SaleId(saleId), CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal(saleId, detail!.SaleId.Value);
        Assert.Equal(SaleStatus.Completed, detail.Status);
        Assert.Equal("Juan Pérez", detail.CashierDisplayName);
        Assert.Equal("Caja 1", detail.RegisterName);
        Assert.Equal(30m, detail.Total);

        var line = Assert.Single(detail.Lines);
        Assert.Equal("SKU-001", line.ProductSku);
        Assert.Equal("Producto de prueba", line.ProductName);
        Assert.Equal(3m, line.Quantity);
        Assert.Equal(10m, line.UnitPrice);
        Assert.Equal(30m, line.LineTotal);

        Assert.Equal(2, detail.Payments.Count);
        Assert.Equal(20m, detail.Payments.Single(p => p.Method == PaymentMethod.Cash).Amount);
        Assert.Equal(10m, detail.Payments.Single(p => p.Method == PaymentMethod.Card).Amount);
    }

    // TAREA 25C, sección 21: el detalle de venta debe exponer la referencia/autorización de Manual
    // Card para que el recibo/detalle pueda mostrarla; Cash nunca la tiene.
    [Fact]
    public async Task GetDetailAsyncExposesTheManualCardReference()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context);

        var saleId = Guid.NewGuid();
        var record = new SaleRecord
        {
            Id = saleId,
            OrganizationId = graph.OrganizationId,
            BranchId = graph.BranchId,
            RegisterSessionId = graph.RegisterSessionId,
            CreatedByUserId = graph.UserId,
            Currency = "MXN",
            Status = SaleStatus.Completed,
            CreatedAtUtc = SqliteSeedHelper.DefaultTimestamp,
            CompletedAtUtc = SqliteSeedHelper.DefaultTimestamp,
        };
        record.Lines.Add(new SaleLineRecord
        {
            Id = Guid.NewGuid(), SaleId = saleId, ProductId = graph.ProductId, ProductSku = "SKU-001",
            ProductName = "Producto de prueba", Quantity = 1m, UnitPriceAmount = 30m, Currency = "MXN", Sale = record,
        });
        record.Payments.Add(new PaymentRecord
        {
            Id = Guid.NewGuid(), SaleId = saleId, Method = PaymentMethod.Card, Amount = 30m, Currency = "MXN",
            PaidAtUtc = SqliteSeedHelper.DefaultTimestamp, Reference = "AUTH-7788", Sale = record,
        });

        context.Add(record);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var query = new EfSalesHistoryQuery(context);

        var detail = await query.GetDetailAsync(
            new OrganizationId(graph.OrganizationId), new SaleId(saleId), CancellationToken.None);

        Assert.NotNull(detail);
        var payment = Assert.Single(detail!.Payments);
        Assert.Equal(PaymentMethod.Card, payment.Method);
        Assert.Equal("AUTH-7788", payment.Reference);
    }

    // Aislamiento de tenant (TAREA 25B, sección 28/43): un SaleId real de otra Organization nunca
    // se materializa, ni siquiera para confirmar que existe.
    [Fact]
    public async Task GetDetailAsyncReturnsNullForASaleBelongingToAnotherOrganization()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graphA = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context, organizationId: Guid.NewGuid());
        var graphB = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context, organizationId: Guid.NewGuid());

        var saleInB = await SeedCompletedSaleAsync(
            context, graphB.OrganizationId, graphB.BranchId, graphB.RegisterSessionId, graphB.UserId,
            SqliteSeedHelper.DefaultTimestamp,
            [(graphB.ProductId, "SKU-001", "Producto", 1m, 10m)], [(PaymentMethod.Cash, 10m)]);

        var query = new EfSalesHistoryQuery(context);

        var detail = await query.GetDetailAsync(
            new OrganizationId(graphA.OrganizationId), new SaleId(saleInB), CancellationToken.None);

        Assert.Null(detail);
    }

    [Fact]
    public async Task GetDetailAsyncReturnsNullWhenTheSaleDoesNotExist()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context);
        var query = new EfSalesHistoryQuery(context);

        var detail = await query.GetDetailAsync(
            new OrganizationId(graph.OrganizationId), SaleId.New(), CancellationToken.None);

        Assert.Null(detail);
    }

    // REQUISITO EXPLÍCITO (TAREA 25B, sección 39): el detalle histórico debe seguir mostrando el
    // snapshot de SaleLine (Sku/Name/UnitPrice) incluso después de que el Product actual cambie.
    [Fact]
    public async Task GetDetailAsyncKeepsShowingTheHistoricalLineSnapshotAfterTheProductChanges()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context);

        var saleId = await SeedCompletedSaleAsync(
            context, graph.OrganizationId, graph.BranchId, graph.RegisterSessionId, graph.UserId,
            SqliteSeedHelper.DefaultTimestamp,
            [(graph.ProductId, "TEST-001", "Agua 1L", 1m, 20m)],
            [(PaymentMethod.Cash, 20m)]);

        // El Product actual cambia después de la venta: nombre y precio de venta actuales.
        var product = await context.Products.SingleAsync(p => p.Id == graph.ProductId);
        product.Name = "Agua Cristal 1L";
        product.SalePriceAmount = 25m;
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var query = new EfSalesHistoryQuery(context);

        var detail = await query.GetDetailAsync(
            new OrganizationId(graph.OrganizationId), new SaleId(saleId), CancellationToken.None);

        Assert.NotNull(detail);
        var line = Assert.Single(detail!.Lines);
        Assert.Equal("TEST-001", line.ProductSku);
        Assert.Equal("Agua 1L", line.ProductName);
        Assert.Equal(20m, line.UnitPrice);
    }

    // ---------- Opciones de filtro ----------

    [Fact]
    public async Task GetFilterOptionsAsyncOnlyReturnsCashiersAndRegistersWithCompletedSalesInTheOrganization()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graphA = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context, organizationId: Guid.NewGuid());
        var graphB = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context, organizationId: Guid.NewGuid());

        await SeedCompletedSaleAsync(
            context, graphA.OrganizationId, graphA.BranchId, graphA.RegisterSessionId, graphA.UserId,
            SqliteSeedHelper.DefaultTimestamp,
            [(graphA.ProductId, "SKU-001", "Producto", 1m, 10m)], [(PaymentMethod.Cash, 10m)]);

        // Draft: no debe generar opciones (sólo Completed cuenta como "vendió algo").
        await SeedDraftSaleAsync(context, graphA.OrganizationId, graphA.BranchId, graphA.RegisterSessionId, graphA.UserId, graphA.ProductId);

        await SeedCompletedSaleAsync(
            context, graphB.OrganizationId, graphB.BranchId, graphB.RegisterSessionId, graphB.UserId,
            SqliteSeedHelper.DefaultTimestamp,
            [(graphB.ProductId, "SKU-001", "Producto", 1m, 10m)], [(PaymentMethod.Cash, 10m)]);

        var query = new EfSalesHistoryQuery(context);

        var options = await query.GetFilterOptionsAsync(new OrganizationId(graphA.OrganizationId), CancellationToken.None);

        var cashier = Assert.Single(options.Cashiers);
        Assert.Equal(graphA.UserId, cashier.UserId.Value);

        var register = Assert.Single(options.Registers);
        Assert.Equal(graphA.RegisterId, register.RegisterId.Value);
    }
}
