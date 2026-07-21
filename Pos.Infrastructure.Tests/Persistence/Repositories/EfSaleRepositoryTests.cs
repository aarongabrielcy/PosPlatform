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

    private static async Task<SaleRecord> SeedDraftSaleAsync(
        PosDbContext context,
        Guid? saleId = null,
        decimal quantity = 2m,
        decimal unitPriceAmount = 10m,
        decimal paymentAmount = 20m)
    {
        var id = saleId ?? Guid.NewGuid();

        var record = new SaleRecord
        {
            Id = id,
            OrganizationId = Guid.NewGuid(),
            BranchId = Guid.NewGuid(),
            RegisterSessionId = Guid.NewGuid(),
            CreatedByUserId = Guid.NewGuid(),
            Currency = "MXN",
            Status = SaleStatus.Draft,
            CreatedAtUtc = CreatedAtUtc,
            CompletedAtUtc = null,
        };

        record.Lines.Add(new SaleLineRecord
        {
            Id = Guid.NewGuid(),
            SaleId = id,
            ProductId = Guid.NewGuid(),
            ProductSku = "SKU-001",
            ProductName = "Producto de prueba",
            Quantity = quantity,
            UnitPriceAmount = unitPriceAmount,
            Currency = "MXN",
            Sale = record,
        });

        record.Payments.Add(new PaymentRecord
        {
            Id = Guid.NewGuid(),
            SaleId = id,
            Method = PaymentMethod.Cash,
            Amount = paymentAmount,
            Currency = "MXN",
            PaidAtUtc = CreatedAtUtc,
            Sale = record,
        });

        context.Add(record);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        return record;
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

        var saleId = Guid.NewGuid();
        await SeedDraftSaleAsync(context, saleId);

        var repository = new EfSaleRepository(context);
        var sale = await repository.GetByIdAsync(new SaleId(saleId), CancellationToken.None);
        sale!.AddLine(
            SaleLineId.New(), ProductId.New(), new Sku("SKU-777"), "Producto nuevo", 1m, new Money(5m, "MXN"));

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
