using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Sales.CompleteSale;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Inventory;
using Pos.Domain.Sales;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Persistence.Records;
using Pos.Infrastructure.Persistence.Repositories;
using Pos.Infrastructure.Tests.Persistence;

namespace Pos.Infrastructure.Tests.Integration;

public class CompleteSaleIntegrationTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CompletedAtUtc = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    private static PosDbContext CreateContext(SqliteConnection connection)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PosDbContext>();
        optionsBuilder.UseSqlite(connection);

        return new PosDbContext(optionsBuilder.Options);
    }

    [Fact]
    public async Task CompleteSaleHandlerPersistsSaleInventoryAndMovementInASingleCommit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        // Sale/SaleLine/InventoryItem tienen FK Restrict hacia su catálogo: se siembra un grafo
        // consistente (misma Organization/Branch/Product) antes de crear la venta y el stock.
        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context, CreatedAtUtc);
        var saleId = Guid.NewGuid();
        var branchId = graph.BranchId;
        var productId = graph.ProductId;
        var lineId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var inventoryItemId = Guid.NewGuid();

        var saleRecord = new SaleRecord
        {
            Id = saleId,
            OrganizationId = graph.OrganizationId,
            BranchId = branchId,
            RegisterSessionId = graph.RegisterSessionId,
            CreatedByUserId = graph.UserId,
            Currency = "MXN",
            Status = SaleStatus.Draft,
            CreatedAtUtc = CreatedAtUtc,
            CompletedAtUtc = null,
        };

        saleRecord.Lines.Add(new SaleLineRecord
        {
            Id = lineId,
            SaleId = saleId,
            ProductId = productId,
            ProductSku = "SKU-001",
            ProductName = "Producto de prueba",
            Quantity = 2m,
            UnitPriceAmount = 10m,
            Currency = "MXN",
            Sale = saleRecord,
        });

        saleRecord.Payments.Add(new PaymentRecord
        {
            Id = paymentId,
            SaleId = saleId,
            Method = PaymentMethod.Cash,
            Amount = 20m,
            Currency = "MXN",
            PaidAtUtc = CreatedAtUtc,
            Sale = saleRecord,
        });

        context.Add(saleRecord);
        context.Add(new InventoryItemRecord
        {
            Id = inventoryItemId,
            BranchId = branchId,
            ProductId = productId,
            Quantity = 10m,
            ReorderPoint = 1m,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = CreatedAtUtc,
        });

        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var saleRepository = new EfSaleRepository(context);
        var inventoryItemRepository = new EfInventoryItemRepository(context);
        var inventoryMovementRepository = new EfInventoryMovementRepository(context);
        var handler = new CompleteSaleHandler(
            saleRepository, inventoryItemRepository, inventoryMovementRepository, context);

        var performedByUserId = UserId.New();
        var command = new CompleteSaleCommand(new SaleId(saleId), performedByUserId, CompletedAtUtc);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        context.ChangeTracker.Clear();

        Assert.Equal(SaleStatus.Completed, result.Status);
        Assert.Equal(CompletedAtUtc, result.CompletedAtUtc);
        Assert.Equal(1, result.InventoryMovementsCreated);

        var reloadedSale = await context.Set<SaleRecord>()
            .Include(r => r.Lines)
            .Include(r => r.Payments)
            .SingleAsync(r => r.Id == saleId);
        Assert.Equal(SaleStatus.Completed, reloadedSale.Status);
        Assert.Equal(CompletedAtUtc, reloadedSale.CompletedAtUtc);
        Assert.Single(reloadedSale.Lines);
        Assert.Equal(lineId, reloadedSale.Lines.Single().Id);
        Assert.Single(reloadedSale.Payments);
        Assert.Equal(paymentId, reloadedSale.Payments.Single().Id);

        var reloadedItem = await context.Set<InventoryItemRecord>().SingleAsync(r => r.Id == inventoryItemId);
        Assert.Equal(8m, reloadedItem.Quantity);

        var reloadedMovement = await context.Set<InventoryMovementRecord>()
            .SingleAsync(r => r.InventoryItemId == inventoryItemId);
        Assert.Equal(InventoryMovementType.SaleDecrease, reloadedMovement.Type);
        Assert.Equal(10m, reloadedMovement.QuantityBefore);
        Assert.Equal(8m, reloadedMovement.QuantityAfter);
        Assert.Equal(saleId, reloadedMovement.SaleId);
        Assert.Equal(lineId, reloadedMovement.SaleLineId);
        Assert.Equal(performedByUserId.Value, reloadedMovement.PerformedByUserId);
    }
}
