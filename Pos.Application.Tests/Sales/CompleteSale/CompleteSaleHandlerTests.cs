using Pos.Application.Common.Exceptions;
using Pos.Application.Sales;
using Pos.Application.Sales.CompleteSale;
using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Common.ValueObjects;
using Pos.Domain.Inventory;
using Pos.Domain.Products;
using Pos.Domain.Sales;

namespace Pos.Application.Tests.Sales.CompleteSale;

public class CompleteSaleHandlerTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    // ---------- Escenarios exitosos ----------

    [Fact]
    public async Task CompletesSaleAndAppliesSingleSaleDecreaseForSimpleScenario()
    {
        var branchId = BranchId.New();
        var productId = ProductId.New();
        var sale = CreateDraftSale(branchId: branchId);
        AddLine(sale, productId: productId, quantity: 2m, unitPrice: new Money(10m, "MXN"));
        Pay(sale, new Money(20m, "MXN"));

        var inventoryItem = CreateInventoryItem(branchId, productId, 10m);

        var saleRepository = new FakeSaleRepository(sale);
        var inventoryItemRepository = new FakeInventoryItemRepository();
        inventoryItemRepository.Add(inventoryItem);
        var movementRepository = new FakeInventoryMovementRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CompleteSaleHandler(saleRepository, inventoryItemRepository, movementRepository, unitOfWork);

        var performedByUserId = UserId.New();
        var completedAtUtc = CreatedAtUtc.AddMinutes(5);
        var command = new CompleteSaleCommand(sale.Id, performedByUserId, completedAtUtc);

        var result = await handler.HandleAsync(command);

        Assert.Equal(SaleStatus.Completed, sale.Status);
        Assert.Equal(completedAtUtc, sale.CompletedAtUtc);
        Assert.Equal(8m, inventoryItem.Quantity);

        var movement = Assert.Single(movementRepository.AddedMovements);
        Assert.Equal(InventoryMovementType.SaleDecrease, movement.Type);
        Assert.Equal(10m, movement.QuantityBefore);
        Assert.Equal(8m, movement.QuantityAfter);
        Assert.Equal(sale.Id, movement.SaleId);
        Assert.Equal(sale.Lines.Single().Id, movement.SaleLineId);
        Assert.Equal(productId, movement.ProductId);
        Assert.Equal(branchId, movement.BranchId);
        Assert.Equal(performedByUserId, movement.PerformedByUserId);
        Assert.Equal(completedAtUtc, movement.OccurredAtUtc);

        Assert.Equal(1, saleRepository.GetByIdCallCount);
        Assert.Equal(1, saleRepository.UpdateCallCount);
        Assert.Equal(1, inventoryItemRepository.GetByBranchAndProductCallCount);
        Assert.Equal(1, inventoryItemRepository.UpdateCallCount);
        Assert.Equal(1, movementRepository.AddCallCount);
        Assert.Equal(1, unitOfWork.CommitCallCount);

        Assert.Equal(sale.Id, result.SaleId);
        Assert.Equal(SaleStatus.Completed, result.Status);
        Assert.Equal(completedAtUtc, result.CompletedAtUtc);
        Assert.Equal(1, result.InventoryMovementsCreated);
    }

    [Fact]
    public async Task CreatesOneMovementPerLineAndCompletesSaleForMultipleLines()
    {
        var branchId = BranchId.New();
        var productId1 = ProductId.New();
        var productId2 = ProductId.New();
        var sale = CreateDraftSale(branchId: branchId);
        AddLine(sale, productId: productId1, quantity: 2m, unitPrice: new Money(10m, "MXN"));
        AddLine(sale, productId: productId2, quantity: 3m, unitPrice: new Money(5m, "MXN"));
        Pay(sale, new Money(35m, "MXN"));

        var item1 = CreateInventoryItem(branchId, productId1, 10m);
        var item2 = CreateInventoryItem(branchId, productId2, 20m);

        var saleRepository = new FakeSaleRepository(sale);
        var inventoryItemRepository = new FakeInventoryItemRepository();
        inventoryItemRepository.Add(item1);
        inventoryItemRepository.Add(item2);
        var movementRepository = new FakeInventoryMovementRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CompleteSaleHandler(saleRepository, inventoryItemRepository, movementRepository, unitOfWork);

        var command = new CompleteSaleCommand(sale.Id, UserId.New(), CreatedAtUtc);

        var result = await handler.HandleAsync(command);

        Assert.Equal(SaleStatus.Completed, sale.Status);
        Assert.Equal(2, movementRepository.AddedMovements.Count);
        Assert.Equal(8m, item1.Quantity);
        Assert.Equal(17m, item2.Quantity);

        Assert.Equal(1, saleRepository.GetByIdCallCount);
        Assert.Equal(1, saleRepository.UpdateCallCount);
        Assert.Equal(2, inventoryItemRepository.GetByBranchAndProductCallCount);
        Assert.Equal(2, inventoryItemRepository.UpdateCallCount);
        Assert.Equal(2, movementRepository.AddCallCount);
        Assert.Equal(1, unitOfWork.CommitCallCount);

        Assert.Equal(2, result.InventoryMovementsCreated);
    }

    // ---------- Rechazos de Command ----------

    [Fact]
    public async Task NullCommandThrowsDomainValidationExceptionWithoutCallingRepositories()
    {
        var (saleRepository, inventoryItemRepository, movementRepository, unitOfWork, handler) = CreateEmptyHandler();

        await Assert.ThrowsAsync<DomainValidationException>(() => handler.HandleAsync(null!));

        Assert.Equal(0, saleRepository.GetByIdCallCount);
        AssertNothingPersisted(saleRepository, inventoryItemRepository, movementRepository, unitOfWork);
    }

    [Fact]
    public async Task DefaultSaleIdThrowsDomainValidationExceptionWithoutCallingRepositories()
    {
        var (saleRepository, inventoryItemRepository, movementRepository, unitOfWork, handler) = CreateEmptyHandler();
        var command = new CompleteSaleCommand(default, UserId.New(), CreatedAtUtc);

        await Assert.ThrowsAsync<DomainValidationException>(() => handler.HandleAsync(command));

        Assert.Equal(0, saleRepository.GetByIdCallCount);
        AssertNothingPersisted(saleRepository, inventoryItemRepository, movementRepository, unitOfWork);
    }

    [Fact]
    public async Task DefaultPerformedByUserIdThrowsDomainValidationExceptionWithoutCallingRepositories()
    {
        var (saleRepository, inventoryItemRepository, movementRepository, unitOfWork, handler) = CreateEmptyHandler();
        var command = new CompleteSaleCommand(SaleId.New(), default, CreatedAtUtc);

        await Assert.ThrowsAsync<DomainValidationException>(() => handler.HandleAsync(command));

        Assert.Equal(0, saleRepository.GetByIdCallCount);
        AssertNothingPersisted(saleRepository, inventoryItemRepository, movementRepository, unitOfWork);
    }

    [Fact]
    public async Task NonUtcCompletedAtUtcThrowsDomainValidationExceptionWithoutCallingRepositories()
    {
        var (saleRepository, inventoryItemRepository, movementRepository, unitOfWork, handler) = CreateEmptyHandler();
        var nonUtc = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.FromHours(-5));
        var command = new CompleteSaleCommand(SaleId.New(), UserId.New(), nonUtc);

        await Assert.ThrowsAsync<DomainValidationException>(() => handler.HandleAsync(command));

        Assert.Equal(0, saleRepository.GetByIdCallCount);
        AssertNothingPersisted(saleRepository, inventoryItemRepository, movementRepository, unitOfWork);
    }

    // ---------- Rechazos de Sale ----------

    [Fact]
    public async Task SaleNotFoundThrowsEntityNotFoundExceptionWithoutCallingOtherRepositories()
    {
        var saleRepository = new FakeSaleRepository(null);
        var inventoryItemRepository = new FakeInventoryItemRepository();
        var movementRepository = new FakeInventoryMovementRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CompleteSaleHandler(saleRepository, inventoryItemRepository, movementRepository, unitOfWork);

        var command = new CompleteSaleCommand(SaleId.New(), UserId.New(), CreatedAtUtc);

        await Assert.ThrowsAsync<EntityNotFoundException>(() => handler.HandleAsync(command));

        Assert.Equal(1, saleRepository.GetByIdCallCount);
        Assert.Equal(0, inventoryItemRepository.GetByBranchAndProductCallCount);
        AssertNothingPersisted(saleRepository, inventoryItemRepository, movementRepository, unitOfWork);
    }

    [Fact]
    public async Task SaleAlreadyCompletedThrowsDomainValidationExceptionWithoutMutatingInventory()
    {
        var branchId = BranchId.New();
        var productId = ProductId.New();
        var sale = CreateDraftSale(branchId: branchId);
        AddLine(sale, productId: productId, quantity: 1m, unitPrice: new Money(10m, "MXN"));
        Pay(sale, new Money(10m, "MXN"));
        sale.Complete(CreatedAtUtc);

        var item = CreateInventoryItem(branchId, productId, 10m);
        var saleRepository = new FakeSaleRepository(sale);
        var inventoryItemRepository = new FakeInventoryItemRepository();
        inventoryItemRepository.Add(item);
        var movementRepository = new FakeInventoryMovementRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CompleteSaleHandler(saleRepository, inventoryItemRepository, movementRepository, unitOfWork);

        var command = new CompleteSaleCommand(sale.Id, UserId.New(), CreatedAtUtc);

        await Assert.ThrowsAsync<DomainValidationException>(() => handler.HandleAsync(command));

        Assert.Equal(0, inventoryItemRepository.GetByBranchAndProductCallCount);
        Assert.Equal(10m, item.Quantity);
        AssertNothingPersisted(saleRepository, inventoryItemRepository, movementRepository, unitOfWork);
    }

    [Fact]
    public async Task SaleWithoutLinesThrowsDomainValidationException()
    {
        var sale = CreateDraftSale();
        var saleRepository = new FakeSaleRepository(sale);
        var inventoryItemRepository = new FakeInventoryItemRepository();
        var movementRepository = new FakeInventoryMovementRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CompleteSaleHandler(saleRepository, inventoryItemRepository, movementRepository, unitOfWork);

        var command = new CompleteSaleCommand(sale.Id, UserId.New(), CreatedAtUtc);

        await Assert.ThrowsAsync<DomainValidationException>(() => handler.HandleAsync(command));

        Assert.Equal(SaleStatus.Draft, sale.Status);
        Assert.Null(sale.CompletedAtUtc);
        AssertNothingPersisted(saleRepository, inventoryItemRepository, movementRepository, unitOfWork);
    }

    [Fact]
    public async Task InsufficientPaymentThrowsDomainValidationExceptionWithoutCallingInventoryRepository()
    {
        var branchId = BranchId.New();
        var productId = ProductId.New();
        var sale = CreateDraftSale(branchId: branchId);
        AddLine(sale, productId: productId, quantity: 2m, unitPrice: new Money(10m, "MXN"));
        Pay(sale, new Money(5m, "MXN"));

        var item = CreateInventoryItem(branchId, productId, 10m);
        var saleRepository = new FakeSaleRepository(sale);
        var inventoryItemRepository = new FakeInventoryItemRepository();
        inventoryItemRepository.Add(item);
        var movementRepository = new FakeInventoryMovementRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CompleteSaleHandler(saleRepository, inventoryItemRepository, movementRepository, unitOfWork);

        var command = new CompleteSaleCommand(sale.Id, UserId.New(), CreatedAtUtc);

        await Assert.ThrowsAsync<DomainValidationException>(() => handler.HandleAsync(command));

        Assert.Equal(SaleStatus.Draft, sale.Status);
        Assert.Equal(0, inventoryItemRepository.GetByBranchAndProductCallCount);
        AssertNothingPersisted(saleRepository, inventoryItemRepository, movementRepository, unitOfWork);
    }

    [Fact]
    public async Task CompletedAtUtcBeforeCreatedAtUtcThrowsDomainValidationException()
    {
        var branchId = BranchId.New();
        var productId = ProductId.New();
        var sale = CreateDraftSale(branchId: branchId);
        AddLine(sale, productId: productId, quantity: 1m, unitPrice: new Money(10m, "MXN"));
        Pay(sale, new Money(10m, "MXN"));

        var item = CreateInventoryItem(branchId, productId, 10m);
        var saleRepository = new FakeSaleRepository(sale);
        var inventoryItemRepository = new FakeInventoryItemRepository();
        inventoryItemRepository.Add(item);
        var movementRepository = new FakeInventoryMovementRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CompleteSaleHandler(saleRepository, inventoryItemRepository, movementRepository, unitOfWork);

        var beforeCreation = CreatedAtUtc.AddMinutes(-1);
        var command = new CompleteSaleCommand(sale.Id, UserId.New(), beforeCreation);

        await Assert.ThrowsAsync<DomainValidationException>(() => handler.HandleAsync(command));

        Assert.Equal(SaleStatus.Draft, sale.Status);
        AssertNothingPersisted(saleRepository, inventoryItemRepository, movementRepository, unitOfWork);
    }

    // ---------- Rechazos de Inventory ----------

    [Fact]
    public async Task InventoryItemNotFoundThrowsEntityNotFoundExceptionWithoutMutatingSale()
    {
        var branchId = BranchId.New();
        var productId = ProductId.New();
        var sale = CreateDraftSale(branchId: branchId);
        AddLine(sale, productId: productId, quantity: 1m, unitPrice: new Money(10m, "MXN"));
        Pay(sale, new Money(10m, "MXN"));

        var saleRepository = new FakeSaleRepository(sale);
        var inventoryItemRepository = new FakeInventoryItemRepository();
        var movementRepository = new FakeInventoryMovementRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CompleteSaleHandler(saleRepository, inventoryItemRepository, movementRepository, unitOfWork);

        var command = new CompleteSaleCommand(sale.Id, UserId.New(), CreatedAtUtc);

        await Assert.ThrowsAsync<EntityNotFoundException>(() => handler.HandleAsync(command));

        Assert.Equal(SaleStatus.Draft, sale.Status);
        AssertNothingPersisted(saleRepository, inventoryItemRepository, movementRepository, unitOfWork);
    }

    [Fact]
    public async Task InventoryItemBranchIdMismatchThrowsDomainValidationException()
    {
        var branchId = BranchId.New();
        var otherBranchId = BranchId.New();
        var productId = ProductId.New();
        var sale = CreateDraftSale(branchId: branchId);
        AddLine(sale, productId: productId, quantity: 1m, unitPrice: new Money(10m, "MXN"));
        Pay(sale, new Money(10m, "MXN"));

        var mismatchedItem = CreateInventoryItem(otherBranchId, productId, 10m);
        var saleRepository = new FakeSaleRepository(sale);
        var inventoryItemRepository = new FakeInventoryItemRepository();
        inventoryItemRepository.AddAt(branchId, productId, mismatchedItem);
        var movementRepository = new FakeInventoryMovementRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CompleteSaleHandler(saleRepository, inventoryItemRepository, movementRepository, unitOfWork);

        var command = new CompleteSaleCommand(sale.Id, UserId.New(), CreatedAtUtc);

        await Assert.ThrowsAsync<DomainValidationException>(() => handler.HandleAsync(command));

        Assert.Equal(SaleStatus.Draft, sale.Status);
        Assert.Equal(10m, mismatchedItem.Quantity);
        AssertNothingPersisted(saleRepository, inventoryItemRepository, movementRepository, unitOfWork);
    }

    [Fact]
    public async Task InventoryItemProductIdMismatchThrowsDomainValidationException()
    {
        var branchId = BranchId.New();
        var productId = ProductId.New();
        var otherProductId = ProductId.New();
        var sale = CreateDraftSale(branchId: branchId);
        AddLine(sale, productId: productId, quantity: 1m, unitPrice: new Money(10m, "MXN"));
        Pay(sale, new Money(10m, "MXN"));

        var mismatchedItem = CreateInventoryItem(branchId, otherProductId, 10m);
        var saleRepository = new FakeSaleRepository(sale);
        var inventoryItemRepository = new FakeInventoryItemRepository();
        inventoryItemRepository.AddAt(branchId, productId, mismatchedItem);
        var movementRepository = new FakeInventoryMovementRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CompleteSaleHandler(saleRepository, inventoryItemRepository, movementRepository, unitOfWork);

        var command = new CompleteSaleCommand(sale.Id, UserId.New(), CreatedAtUtc);

        await Assert.ThrowsAsync<DomainValidationException>(() => handler.HandleAsync(command));

        Assert.Equal(SaleStatus.Draft, sale.Status);
        Assert.Equal(10m, mismatchedItem.Quantity);
        AssertNothingPersisted(saleRepository, inventoryItemRepository, movementRepository, unitOfWork);
    }

    [Fact]
    public async Task InsufficientInventoryForLineThrowsDomainValidationException()
    {
        var branchId = BranchId.New();
        var productId = ProductId.New();
        var sale = CreateDraftSale(branchId: branchId);
        AddLine(sale, productId: productId, quantity: 5m, unitPrice: new Money(10m, "MXN"));
        Pay(sale, new Money(50m, "MXN"));

        var item = CreateInventoryItem(branchId, productId, 3m);
        var saleRepository = new FakeSaleRepository(sale);
        var inventoryItemRepository = new FakeInventoryItemRepository();
        inventoryItemRepository.Add(item);
        var movementRepository = new FakeInventoryMovementRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CompleteSaleHandler(saleRepository, inventoryItemRepository, movementRepository, unitOfWork);

        var command = new CompleteSaleCommand(sale.Id, UserId.New(), CreatedAtUtc);

        await Assert.ThrowsAsync<DomainValidationException>(() => handler.HandleAsync(command));

        Assert.Equal(3m, item.Quantity);
        Assert.Equal(SaleStatus.Draft, sale.Status);
        AssertNothingPersisted(saleRepository, inventoryItemRepository, movementRepository, unitOfWork);
    }

    [Fact]
    public async Task DuplicateInventoryItemIdAcrossLinesThrowsDomainValidationException()
    {
        var branchId = BranchId.New();
        var productId1 = ProductId.New();
        var productId2 = ProductId.New();
        var sale = CreateDraftSale(branchId: branchId);
        AddLine(sale, productId: productId1, quantity: 1m, unitPrice: new Money(10m, "MXN"));
        AddLine(sale, productId: productId2, quantity: 1m, unitPrice: new Money(10m, "MXN"));
        Pay(sale, new Money(20m, "MXN"));

        var sharedId = InventoryItemId.New();
        var item1 = new InventoryItem(sharedId, branchId, productId1, 10m, 0m, CreatedAtUtc);
        var item2 = new InventoryItem(sharedId, branchId, productId2, 10m, 0m, CreatedAtUtc);

        var saleRepository = new FakeSaleRepository(sale);
        var inventoryItemRepository = new FakeInventoryItemRepository();
        inventoryItemRepository.Add(item1);
        inventoryItemRepository.Add(item2);
        var movementRepository = new FakeInventoryMovementRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CompleteSaleHandler(saleRepository, inventoryItemRepository, movementRepository, unitOfWork);

        var command = new CompleteSaleCommand(sale.Id, UserId.New(), CreatedAtUtc);

        await Assert.ThrowsAsync<DomainValidationException>(() => handler.HandleAsync(command));

        Assert.Equal(SaleStatus.Draft, sale.Status);
        Assert.Equal(10m, item1.Quantity);
        Assert.Equal(10m, item2.Quantity);
        AssertNothingPersisted(saleRepository, inventoryItemRepository, movementRepository, unitOfWork);
    }

    // ---------- Atomicidad ----------

    [Fact]
    public async Task SecondLineWithInsufficientInventoryDoesNotMutateFirstInventoryItem()
    {
        var branchId = BranchId.New();
        var productId1 = ProductId.New();
        var productId2 = ProductId.New();
        var sale = CreateDraftSale(branchId: branchId);
        AddLine(sale, productId: productId1, quantity: 2m, unitPrice: new Money(10m, "MXN"));
        AddLine(sale, productId: productId2, quantity: 5m, unitPrice: new Money(10m, "MXN"));
        Pay(sale, new Money(70m, "MXN"));

        var item1 = CreateInventoryItem(branchId, productId1, 10m);
        var item2 = CreateInventoryItem(branchId, productId2, 3m);

        var saleRepository = new FakeSaleRepository(sale);
        var inventoryItemRepository = new FakeInventoryItemRepository();
        inventoryItemRepository.Add(item1);
        inventoryItemRepository.Add(item2);
        var movementRepository = new FakeInventoryMovementRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CompleteSaleHandler(saleRepository, inventoryItemRepository, movementRepository, unitOfWork);

        var command = new CompleteSaleCommand(sale.Id, UserId.New(), CreatedAtUtc);

        await Assert.ThrowsAsync<DomainValidationException>(() => handler.HandleAsync(command));

        Assert.Equal(10m, item1.Quantity);
        Assert.Equal(item1.CreatedAtUtc, item1.UpdatedAtUtc);
        Assert.Equal(3m, item2.Quantity);
        Assert.Equal(SaleStatus.Draft, sale.Status);
        Assert.Null(sale.CompletedAtUtc);
        AssertNothingPersisted(saleRepository, inventoryItemRepository, movementRepository, unitOfWork);
    }

    [Fact]
    public async Task SecondLineWithCompletedAtUtcBeforeItsInventoryItemUpdatedAtUtcDoesNotMutateFirstInventoryItem()
    {
        var branchId = BranchId.New();
        var productId1 = ProductId.New();
        var productId2 = ProductId.New();
        var sale = CreateDraftSale(branchId: branchId);
        AddLine(sale, productId: productId1, quantity: 1m, unitPrice: new Money(10m, "MXN"));
        AddLine(sale, productId: productId2, quantity: 1m, unitPrice: new Money(10m, "MXN"));
        Pay(sale, new Money(20m, "MXN"));

        var item1 = new InventoryItem(InventoryItemId.New(), branchId, productId1, 10m, 0m, CreatedAtUtc.AddHours(-1));
        var item2 = new InventoryItem(InventoryItemId.New(), branchId, productId2, 10m, 0m, CreatedAtUtc.AddHours(1));

        var saleRepository = new FakeSaleRepository(sale);
        var inventoryItemRepository = new FakeInventoryItemRepository();
        inventoryItemRepository.Add(item1);
        inventoryItemRepository.Add(item2);
        var movementRepository = new FakeInventoryMovementRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CompleteSaleHandler(saleRepository, inventoryItemRepository, movementRepository, unitOfWork);

        var command = new CompleteSaleCommand(sale.Id, UserId.New(), CreatedAtUtc);

        await Assert.ThrowsAsync<DomainValidationException>(() => handler.HandleAsync(command));

        Assert.Equal(10m, item1.Quantity);
        Assert.Equal(item1.CreatedAtUtc, item1.UpdatedAtUtc);
        Assert.Equal(10m, item2.Quantity);
        Assert.Equal(item2.CreatedAtUtc, item2.UpdatedAtUtc);
        Assert.Equal(SaleStatus.Draft, sale.Status);
        Assert.Null(sale.CompletedAtUtc);
        AssertNothingPersisted(saleRepository, inventoryItemRepository, movementRepository, unitOfWork);
    }

    [Fact]
    public async Task CompletingSaleWritesInDeterministicOrderWithCommitLast()
    {
        var branchId = BranchId.New();
        var productId1 = ProductId.New();
        var productId2 = ProductId.New();
        var sale = CreateDraftSale(branchId: branchId);
        AddLine(sale, productId: productId1, quantity: 2m, unitPrice: new Money(10m, "MXN"));
        AddLine(sale, productId: productId2, quantity: 3m, unitPrice: new Money(5m, "MXN"));
        Pay(sale, new Money(35m, "MXN"));

        var item1 = CreateInventoryItem(branchId, productId1, 10m);
        var item2 = CreateInventoryItem(branchId, productId2, 20m);

        var operationLog = new List<string>();
        var saleRepository = new FakeSaleRepository(sale, operationLog);
        var inventoryItemRepository = new FakeInventoryItemRepository(operationLog);
        inventoryItemRepository.Add(item1);
        inventoryItemRepository.Add(item2);
        var movementRepository = new FakeInventoryMovementRepository(operationLog);
        var unitOfWork = new FakeUnitOfWork(operationLog);
        var handler = new CompleteSaleHandler(saleRepository, inventoryItemRepository, movementRepository, unitOfWork);

        var command = new CompleteSaleCommand(sale.Id, UserId.New(), CreatedAtUtc);

        await handler.HandleAsync(command);

        Assert.Equal(SaleStatus.Completed, sale.Status);
        Assert.Equal(2, operationLog.Count(op => op == "Inventory.Update"));
        Assert.Equal(2, operationLog.Count(op => op == "Movement.Add"));
        Assert.Equal(1, operationLog.Count(op => op == "Sale.Update"));
        Assert.Equal(1, operationLog.Count(op => op == "UnitOfWork.Commit"));

        Assert.Equal("UnitOfWork.Commit", operationLog[^1]);
        Assert.True(operationLog.IndexOf("Sale.Update") < operationLog.IndexOf("UnitOfWork.Commit"));
        Assert.True(operationLog.LastIndexOf("Inventory.Update") < operationLog.IndexOf("Sale.Update"));
        Assert.True(operationLog.LastIndexOf("Movement.Add") < operationLog.IndexOf("Sale.Update"));
    }

    [Fact]
    public async Task CommitIsNotCalledWhenSaleRepositoryUpdateThrows()
    {
        var branchId = BranchId.New();
        var productId = ProductId.New();
        var sale = CreateDraftSale(branchId: branchId);
        AddLine(sale, productId: productId, quantity: 1m, unitPrice: new Money(10m, "MXN"));
        Pay(sale, new Money(10m, "MXN"));

        var item = CreateInventoryItem(branchId, productId, 10m);
        var inventoryItemRepository = new FakeInventoryItemRepository();
        inventoryItemRepository.Add(item);
        var movementRepository = new FakeInventoryMovementRepository();
        var unitOfWork = new FakeUnitOfWork();
        var saleRepository = new ThrowingSaleRepository(sale);
        var handler = new CompleteSaleHandler(saleRepository, inventoryItemRepository, movementRepository, unitOfWork);

        var command = new CompleteSaleCommand(sale.Id, UserId.New(), CreatedAtUtc);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(command));

        Assert.Equal(0, unitOfWork.CommitCallCount);
    }

    private sealed class ThrowingSaleRepository : ISaleRepository
    {
        private readonly Sale _sale;

        public ThrowingSaleRepository(Sale sale)
        {
            _sale = sale;
        }

        public Task<Sale?> GetByIdAsync(SaleId saleId, CancellationToken cancellationToken) =>
            Task.FromResult<Sale?>(_sale.Id == saleId ? _sale : null);

        public Task AddAsync(Sale sale, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Fallo simulado de persistencia.");

        public Task UpdateAsync(Sale sale, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Fallo simulado de persistencia.");

        public Task<decimal> GetCompletedCashTotalByRegisterSessionAsync(
            RegisterSessionId registerSessionId, CancellationToken cancellationToken) =>
            Task.FromResult(0m);
    }

    // ---------- Helpers ----------

    private static Sale CreateDraftSale(BranchId? branchId = null, DateTimeOffset? createdAtUtc = null) =>
        new(
            SaleId.New(),
            OrganizationId.New(),
            branchId ?? BranchId.New(),
            RegisterSessionId.New(),
            UserId.New(),
            "MXN",
            createdAtUtc ?? CreatedAtUtc);

    private static void AddLine(
        Sale sale,
        ProductId? productId = null,
        decimal quantity = 1m,
        Money? unitPrice = null) =>
        sale.AddLine(
            SaleLineId.New(),
            productId ?? ProductId.New(),
            new Sku("SKU-001"),
            "Producto de prueba",
            quantity,
            unitPrice ?? new Money(10m, "MXN"));

    private static void Pay(Sale sale, Money amount) =>
        sale.AddPayment(PaymentId.New(), PaymentMethod.Cash, amount, sale.CreatedAtUtc);

    private static InventoryItem CreateInventoryItem(BranchId branchId, ProductId productId, decimal quantity) =>
        new(InventoryItemId.New(), branchId, productId, quantity, reorderPoint: 0m, CreatedAtUtc);

    private static (
        FakeSaleRepository SaleRepository,
        FakeInventoryItemRepository InventoryItemRepository,
        FakeInventoryMovementRepository MovementRepository,
        FakeUnitOfWork UnitOfWork,
        CompleteSaleHandler Handler) CreateEmptyHandler()
    {
        var saleRepository = new FakeSaleRepository(null);
        var inventoryItemRepository = new FakeInventoryItemRepository();
        var movementRepository = new FakeInventoryMovementRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CompleteSaleHandler(saleRepository, inventoryItemRepository, movementRepository, unitOfWork);

        return (saleRepository, inventoryItemRepository, movementRepository, unitOfWork, handler);
    }

    private static void AssertNothingPersisted(
        FakeSaleRepository saleRepository,
        FakeInventoryItemRepository inventoryItemRepository,
        FakeInventoryMovementRepository movementRepository,
        FakeUnitOfWork unitOfWork)
    {
        Assert.Equal(0, saleRepository.UpdateCallCount);
        Assert.Equal(0, inventoryItemRepository.UpdateCallCount);
        Assert.Equal(0, movementRepository.AddCallCount);
        Assert.Equal(0, unitOfWork.CommitCallCount);
    }
}
