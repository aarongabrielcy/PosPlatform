using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Exceptions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Common.ValueObjects;
using Pos.Domain.Products;
using Pos.Domain.Sales;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Persistence.Exceptions;
using Pos.Infrastructure.Persistence.Records;
using Pos.Infrastructure.Persistence.Repositories;
using Pos.Infrastructure.Tests.Persistence;

namespace Pos.Infrastructure.Tests.Persistence.Repositories;

public class EfSaleRepositoryTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CompletedAtUtc = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    private static PosDbContext CreateContext(SqliteConnection connection)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PosDbContext>();
        optionsBuilder.UseSqlite(connection);

        return new PosDbContext(optionsBuilder.Options);
    }

    // Sale/SaleLine tienen FK Restrict hacia Organization/Branch/RegisterSession/User/Product:
    // se siembra primero el grafo completo de catálogo con SqliteSeedHelper.
    private static async Task<SaleRecord> SeedDraftSaleAsync(
        PosDbContext context,
        Guid? saleId = null,
        decimal quantity = 2m,
        decimal unitPriceAmount = 10m,
        decimal paymentAmount = 20m)
    {
        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context, CreatedAtUtc);

        return await SqliteSeedHelper.SeedSaleAsync(
            context,
            graph.OrganizationId,
            graph.BranchId,
            graph.RegisterSessionId,
            graph.UserId,
            graph.ProductId,
            saleId: saleId,
            quantity: quantity,
            unitPriceAmount: unitPriceAmount,
            paymentAmount: paymentAmount,
            createdAtUtc: CreatedAtUtc);
    }

    // ---------- GetByIdAsync ----------

    [Fact]
    public async Task GetByIdAsyncReturnsNullWhenNotFound()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var repository = new EfSaleRepository(context);

        var result = await repository.GetByIdAsync(SaleId.New(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsyncReconstructsDraftSaleWithLinesAndPayments()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var saleId = Guid.NewGuid();
        var seeded = await SeedDraftSaleAsync(context, saleId);

        var repository = new EfSaleRepository(context);
        var sale = await repository.GetByIdAsync(new SaleId(saleId), CancellationToken.None);

        Assert.NotNull(sale);
        Assert.Equal(SaleStatus.Draft, sale!.Status);
        Assert.Null(sale.CompletedAtUtc);
        Assert.Single(sale.Lines);
        Assert.Single(sale.Payments);
        Assert.Equal(seeded.Lines.Single().Id, sale.Lines.Single().Id.Value);
        Assert.Equal(seeded.Payments.Single().Id, sale.Payments.Single().Id.Value);
    }

    [Fact]
    public async Task GetByIdAsyncReconstructsCompletedSale()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var saleId = Guid.NewGuid();
        await SeedDraftSaleAsync(context, saleId);

        var completedRecord = await context.Set<SaleRecord>().SingleAsync(r => r.Id == saleId);
        completedRecord.Status = SaleStatus.Completed;
        completedRecord.CompletedAtUtc = CompletedAtUtc;
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var repository = new EfSaleRepository(context);
        var sale = await repository.GetByIdAsync(new SaleId(saleId), CancellationToken.None);

        Assert.NotNull(sale);
        Assert.Equal(SaleStatus.Completed, sale!.Status);
        Assert.Equal(CompletedAtUtc, sale.CompletedAtUtc);
    }

    [Fact]
    public async Task GetByIdAsyncDoesNotTrackTheReturnedRecord()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var saleId = Guid.NewGuid();
        await SeedDraftSaleAsync(context, saleId);

        var repository = new EfSaleRepository(context);
        await repository.GetByIdAsync(new SaleId(saleId), CancellationToken.None);

        Assert.Empty(context.ChangeTracker.Entries<SaleRecord>());
    }

    [Fact]
    public async Task GetByIdAsyncPropagatesCancellation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var repository = new EfSaleRepository(context);
        using var cancelledSource = new CancellationTokenSource();
        cancelledSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => repository.GetByIdAsync(SaleId.New(), cancelledSource.Token));
    }

    // ---------- AddAsync (TAREA 25A: CheckoutService crea la Sale, no la busca por Id) ----------

    [Fact]
    public async Task AddAsyncPersistsANewCompletedSaleWithLinesAndPayments()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context, CreatedAtUtc);

        var sale = new Sale(
            SaleId.New(), new OrganizationId(graph.OrganizationId), new BranchId(graph.BranchId),
            new RegisterSessionId(graph.RegisterSessionId), new UserId(graph.UserId), "MXN", CreatedAtUtc);
        sale.AddLine(
            SaleLineId.New(), new ProductId(graph.ProductId), new Sku("SKU-001"), "Producto de prueba", 2m,
            new Money(10m, "MXN"));
        sale.AddPayment(PaymentId.New(), PaymentMethod.Cash, new Money(20m, "MXN"), CreatedAtUtc);
        sale.Complete(CompletedAtUtc);

        var repository = new EfSaleRepository(context);
        await repository.AddAsync(sale, CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var reloaded = await context.Set<SaleRecord>()
            .Include(r => r.Lines)
            .Include(r => r.Payments)
            .SingleAsync(r => r.Id == sale.Id.Value);

        Assert.Equal(SaleStatus.Completed, reloaded.Status);
        Assert.Equal(CompletedAtUtc, reloaded.CompletedAtUtc);
        var line = Assert.Single(reloaded.Lines);
        Assert.Equal(graph.ProductId, line.ProductId);
        Assert.Equal(2m, line.Quantity);
        var payment = Assert.Single(reloaded.Payments);
        Assert.Equal(PaymentMethod.Cash, payment.Method);
        Assert.Equal(20m, payment.Amount);
    }

    [Fact]
    public async Task AddAsyncDoesNotSaveAutomatically()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context, CreatedAtUtc);

        var sale = new Sale(
            SaleId.New(), new OrganizationId(graph.OrganizationId), new BranchId(graph.BranchId),
            new RegisterSessionId(graph.RegisterSessionId), new UserId(graph.UserId), "MXN", CreatedAtUtc);
        sale.AddLine(
            SaleLineId.New(), new ProductId(graph.ProductId), new Sku("SKU-001"), "Producto de prueba", 1m,
            new Money(10m, "MXN"));

        var repository = new EfSaleRepository(context);
        await repository.AddAsync(sale, CancellationToken.None);

        Assert.Equal(0, await context.Set<SaleRecord>().CountAsync());

        var trackedEntry = context.ChangeTracker.Entries<SaleRecord>().Single(e => e.Entity.Id == sale.Id.Value);
        Assert.Equal(EntityState.Added, trackedEntry.State);
    }

    [Fact]
    public async Task AddAsyncRejectsNullSale()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var repository = new EfSaleRepository(context);

        await Assert.ThrowsAsync<ArgumentNullException>(() => repository.AddAsync(null!, CancellationToken.None));
    }

    // ---------- GetCompletedCashTotalByRegisterSessionAsync (TAREA 25A sección 23) ----------

    [Fact]
    public async Task GetCompletedCashTotalByRegisterSessionAsyncReturnsZeroWhenNoSalesExist()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context, CreatedAtUtc);

        var repository = new EfSaleRepository(context);
        var total = await repository.GetCompletedCashTotalByRegisterSessionAsync(
            new RegisterSessionId(graph.RegisterSessionId), CancellationToken.None);

        Assert.Equal(0m, total);
    }

    [Fact]
    public async Task GetCompletedCashTotalByRegisterSessionAsyncSumsOnlyCompletedCashSalesOfThatSession()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context, CreatedAtUtc);

        // Cuenta: Completed + Cash de la misma sesión.
        await SqliteSeedHelper.SeedSaleAsync(
            context, graph.OrganizationId, graph.BranchId, graph.RegisterSessionId, graph.UserId, graph.ProductId,
            saleId: Guid.NewGuid(), saleLineId: Guid.NewGuid(), paymentId: Guid.NewGuid(),
            paymentAmount: 20m, status: SaleStatus.Completed, completedAtUtc: CompletedAtUtc);

        // No cuenta: Draft, aunque sea Cash y de la misma sesión.
        await SqliteSeedHelper.SeedSaleAsync(
            context, graph.OrganizationId, graph.BranchId, graph.RegisterSessionId, graph.UserId, graph.ProductId,
            saleId: Guid.NewGuid(), saleLineId: Guid.NewGuid(), paymentId: Guid.NewGuid(),
            paymentAmount: 15m, status: SaleStatus.Draft);

        // No cuenta: pago Card, aunque la Sale esté Completed y sea de la misma sesión.
        var cardSaleId = Guid.NewGuid();
        var cardSaleRecord = new SaleRecord
        {
            Id = cardSaleId,
            OrganizationId = graph.OrganizationId,
            BranchId = graph.BranchId,
            RegisterSessionId = graph.RegisterSessionId,
            CreatedByUserId = graph.UserId,
            Currency = "MXN",
            Status = SaleStatus.Completed,
            CreatedAtUtc = CreatedAtUtc,
            CompletedAtUtc = CompletedAtUtc,
        };
        cardSaleRecord.Lines.Add(new SaleLineRecord
        {
            Id = Guid.NewGuid(),
            SaleId = cardSaleId,
            ProductId = graph.ProductId,
            ProductSku = "SKU-001",
            ProductName = "Producto de prueba",
            Quantity = 1m,
            UnitPriceAmount = 999m,
            Currency = "MXN",
            Sale = cardSaleRecord,
        });
        cardSaleRecord.Payments.Add(new PaymentRecord
        {
            Id = Guid.NewGuid(),
            SaleId = cardSaleId,
            Method = PaymentMethod.Card,
            Amount = 999m,
            Currency = "MXN",
            PaidAtUtc = CreatedAtUtc,
            Sale = cardSaleRecord,
        });
        context.Add(cardSaleRecord);
        await context.CommitAsync(CancellationToken.None);

        // No cuenta: Completed + Cash, pero de otra RegisterSession.
        var otherRegisterSessionId = Guid.NewGuid();
        context.Add(new RegisterSessionRecord
        {
            Id = otherRegisterSessionId,
            RegisterId = graph.RegisterId,
            OpenedByUserId = graph.UserId,
            OpeningFloatAmount = 100m,
            OpeningFloatCurrency = "MXN",
            Status = Pos.Domain.RegisterSessions.RegisterSessionStatus.Closed,
            OpenedAtUtc = CreatedAtUtc,
            ClosedByUserId = graph.UserId,
            ExpectedCashAmount = 100m,
            ExpectedCashCurrency = "MXN",
            CountedCashAmount = 100m,
            CountedCashCurrency = "MXN",
            CashDifferenceAmount = 0m,
            CashDifferenceCurrency = "MXN",
            ClosedAtUtc = CompletedAtUtc,
        });
        await context.CommitAsync(CancellationToken.None);
        await SqliteSeedHelper.SeedSaleAsync(
            context, graph.OrganizationId, graph.BranchId, otherRegisterSessionId, graph.UserId, graph.ProductId,
            saleId: Guid.NewGuid(), saleLineId: Guid.NewGuid(), paymentId: Guid.NewGuid(),
            paymentAmount: 999m, status: SaleStatus.Completed, completedAtUtc: CompletedAtUtc);

        context.ChangeTracker.Clear();

        var repository = new EfSaleRepository(context);
        var total = await repository.GetCompletedCashTotalByRegisterSessionAsync(
            new RegisterSessionId(graph.RegisterSessionId), CancellationToken.None);

        Assert.Equal(20m, total);
    }

    // ---------- UpdateAsync ----------

    [Fact]
    public async Task UpdateAsyncCompletesADraftSale()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var saleId = Guid.NewGuid();
        await SeedDraftSaleAsync(context, saleId);

        var repository = new EfSaleRepository(context);
        var sale = await repository.GetByIdAsync(new SaleId(saleId), CancellationToken.None);
        sale!.Complete(CompletedAtUtc);

        await repository.UpdateAsync(sale, CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var reloaded = await context.Set<SaleRecord>().SingleAsync(r => r.Id == saleId);
        Assert.Equal(SaleStatus.Completed, reloaded.Status);
        Assert.Equal(CompletedAtUtc, reloaded.CompletedAtUtc);
    }

    [Fact]
    public async Task UpdateAsyncAddsNewLineWhenSaleIsDraft()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context, CreatedAtUtc);
        var saleId = Guid.NewGuid();
        await SqliteSeedHelper.SeedSaleAsync(
            context,
            graph.OrganizationId,
            graph.BranchId,
            graph.RegisterSessionId,
            graph.UserId,
            graph.ProductId,
            saleId: saleId,
            createdAtUtc: CreatedAtUtc);

        // SaleLine.ProductId tiene FK Restrict hacia Product: la nueva línea necesita un
        // Product real distinto del ya usado en la línea sembrada (índice único SaleId+ProductId).
        var newProductId = Guid.NewGuid();
        context.Add(new ProductRecord
        {
            Id = newProductId,
            OrganizationId = graph.OrganizationId,
            Sku = "SKU-777",
            Name = "Producto nuevo",
            SalePriceAmount = 5m,
            SalePriceCurrency = "MXN",
            TracksInventory = true,
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
        });
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var repository = new EfSaleRepository(context);
        var sale = await repository.GetByIdAsync(new SaleId(saleId), CancellationToken.None);
        sale!.AddLine(
            SaleLineId.New(), new ProductId(newProductId), new Sku("SKU-777"), "Producto nuevo", 1m, new Money(5m, "MXN"));

        await repository.UpdateAsync(sale, CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var reloaded = await context.Set<SaleRecord>()
            .Include(r => r.Lines)
            .SingleAsync(r => r.Id == saleId);
        Assert.Equal(2, reloaded.Lines.Count);
    }

    [Fact]
    public async Task UpdateAsyncAddsNewPayment()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var saleId = Guid.NewGuid();
        await SeedDraftSaleAsync(context, saleId);

        var repository = new EfSaleRepository(context);
        var sale = await repository.GetByIdAsync(new SaleId(saleId), CancellationToken.None);
        sale!.AddPayment(PaymentId.New(), PaymentMethod.Cash, new Money(5m, "MXN"), CreatedAtUtc);

        await repository.UpdateAsync(sale, CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var reloaded = await context.Set<SaleRecord>()
            .Include(r => r.Payments)
            .SingleAsync(r => r.Id == saleId);
        Assert.Equal(2, reloaded.Payments.Count);
    }

    [Fact]
    public async Task UpdateAsyncRemovesAbsentChildWhenSaleIsDraft()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var saleId = Guid.NewGuid();
        await SeedDraftSaleAsync(context, saleId);

        var repository = new EfSaleRepository(context);
        var sale = await repository.GetByIdAsync(new SaleId(saleId), CancellationToken.None);
        var paymentId = sale!.Payments.Single().Id;
        sale.RemovePayment(paymentId);

        await repository.UpdateAsync(sale, CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var reloaded = await context.Set<SaleRecord>()
            .Include(r => r.Payments)
            .SingleAsync(r => r.Id == saleId);
        Assert.Empty(reloaded.Payments);
    }

    [Fact]
    public async Task UpdateAsyncDoesNotSaveAutomatically()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var saleId = Guid.NewGuid();
        await SeedDraftSaleAsync(context, saleId);

        var repository = new EfSaleRepository(context);
        var sale = await repository.GetByIdAsync(new SaleId(saleId), CancellationToken.None);
        sale!.Complete(CompletedAtUtc);

        await repository.UpdateAsync(sale, CancellationToken.None);

        var trackedEntry = context.ChangeTracker.Entries<SaleRecord>().Single(e => e.Entity.Id == saleId);
        Assert.Equal(EntityState.Modified, trackedEntry.State);

        context.ChangeTracker.Clear();

        var reloaded = await context.Set<SaleRecord>().SingleAsync(r => r.Id == saleId);
        Assert.Equal(SaleStatus.Draft, reloaded.Status);
    }

    [Fact]
    public async Task UpdateAsyncDoesNotAlterIdentity()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var saleId = Guid.NewGuid();
        var seeded = await SeedDraftSaleAsync(context, saleId);

        var repository = new EfSaleRepository(context);
        var sale = await repository.GetByIdAsync(new SaleId(saleId), CancellationToken.None);
        sale!.Complete(CompletedAtUtc);

        await repository.UpdateAsync(sale, CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var reloaded = await context.Set<SaleRecord>().SingleAsync(r => r.Id == saleId);
        Assert.Equal(seeded.Id, reloaded.Id);
        Assert.Equal(seeded.OrganizationId, reloaded.OrganizationId);
        Assert.Equal(seeded.BranchId, reloaded.BranchId);
        Assert.Equal(seeded.RegisterSessionId, reloaded.RegisterSessionId);
        Assert.Equal(seeded.CreatedByUserId, reloaded.CreatedByUserId);
        Assert.Equal(seeded.CreatedAtUtc, reloaded.CreatedAtUtc);
    }

    [Fact]
    public async Task UpdateAsyncThrowsEntityNotFoundExceptionWhenRecordDoesNotExist()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var saleId = Guid.NewGuid();
        await SeedDraftSaleAsync(context, saleId);

        var repository = new EfSaleRepository(context);
        var sale = await repository.GetByIdAsync(new SaleId(saleId), CancellationToken.None);
        sale!.Complete(CompletedAtUtc);

        context.ChangeTracker.Clear();

        await using var otherConnection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await otherConnection.OpenAsync();
        await using var otherContext = CreateContext(otherConnection);
        await otherContext.Database.EnsureCreatedAsync();
        var otherRepository = new EfSaleRepository(otherContext);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => otherRepository.UpdateAsync(sale, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAsyncThrowsAndLeavesChangeTrackerUnchangedWhenExistingPaymentIsIncompatible()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var saleId = Guid.NewGuid();
        await SeedDraftSaleAsync(context, saleId);

        var repository = new EfSaleRepository(context);
        var sale = await repository.GetByIdAsync(new SaleId(saleId), CancellationToken.None);

        // Corrompe el pago ya persistido para que ya no coincida con el snapshot del
        // agregado de dominio cargado previamente.
        var persistedRecord = await context.Set<SaleRecord>()
            .Include(r => r.Payments)
            .SingleAsync(r => r.Id == saleId);
        persistedRecord.Payments.Single().Amount = 999m;
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        await Assert.ThrowsAsync<PersistenceDataException>(
            () => repository.UpdateAsync(sale!, CancellationToken.None));

        Assert.All(
            context.ChangeTracker.Entries(),
            entry => Assert.Equal(EntityState.Unchanged, entry.State));

        context.ChangeTracker.Clear();

        var reloaded = await context.Set<SaleRecord>()
            .Include(r => r.Lines)
            .Include(r => r.Payments)
            .SingleAsync(r => r.Id == saleId);
        Assert.Equal(SaleStatus.Draft, reloaded.Status);
        Assert.Null(reloaded.CompletedAtUtc);
        Assert.Single(reloaded.Lines);
        Assert.Single(reloaded.Payments);
        Assert.Equal(999m, reloaded.Payments.Single().Amount);
    }

    [Fact]
    public async Task UpdateAsyncThrowsAndLeavesChangeTrackerUnchangedWhenExistingLineIsIncompatible()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var saleId = Guid.NewGuid();
        await SeedDraftSaleAsync(context, saleId);

        var repository = new EfSaleRepository(context);
        var sale = await repository.GetByIdAsync(new SaleId(saleId), CancellationToken.None);

        // Corrompe el snapshot de la línea ya persistida para que ya no coincida con el
        // snapshot inmutable del agregado de dominio cargado previamente.
        var persistedRecord = await context.Set<SaleRecord>()
            .Include(r => r.Lines)
            .SingleAsync(r => r.Id == saleId);
        persistedRecord.Lines.Single().ProductSku = "SKU-CORRUPTA";
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        await Assert.ThrowsAsync<PersistenceDataException>(
            () => repository.UpdateAsync(sale!, CancellationToken.None));

        Assert.All(
            context.ChangeTracker.Entries(),
            entry => Assert.Equal(EntityState.Unchanged, entry.State));

        context.ChangeTracker.Clear();

        var reloaded = await context.Set<SaleRecord>()
            .Include(r => r.Lines)
            .Include(r => r.Payments)
            .SingleAsync(r => r.Id == saleId);
        Assert.Equal(SaleStatus.Draft, reloaded.Status);
        Assert.Null(reloaded.CompletedAtUtc);
        Assert.Single(reloaded.Lines);
        Assert.Single(reloaded.Payments);
        Assert.Equal("SKU-CORRUPTA", reloaded.Lines.Single().ProductSku);
    }

    [Fact]
    public async Task UpdateAsyncRejectsNullSale()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var repository = new EfSaleRepository(context);

        await Assert.ThrowsAsync<ArgumentNullException>(() => repository.UpdateAsync(null!, CancellationToken.None));
    }

    [Fact]
    public void ConstructorRejectsNullContext()
    {
        Assert.Throws<ArgumentNullException>(() => new EfSaleRepository(null!));
    }
}
