using Pos.Application.Authentication;
using Pos.Application.Products.ManageProduct;
using Pos.Application.RegisterSessions;
using Pos.Application.Tests.Common.Time;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Common.ValueObjects;
using Pos.Domain.Inventory;
using Pos.Domain.Products;
using Pos.Domain.Security;

namespace Pos.Application.Tests.Products.ManageProduct;

public class ProductManagementServiceTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset UtcNow = new(2026, 1, 2, 9, 0, 0, TimeSpan.Zero);

    private sealed record Fixture(
        ProductManagementService Service,
        FakeCurrentUserSession UserSession,
        FakeCurrentRegisterSession RegisterSession,
        FakeProductRepository ProductRepository,
        FakeInventoryItemRepository InventoryItemRepository,
        FakeInventoryMovementRepository InventoryMovementRepository,
        FakeUnitOfWork UnitOfWork,
        OrganizationId OrganizationId,
        BranchId BranchId);

    private static Fixture CreateFixture(
        bool authenticated = true,
        bool registerOpen = true,
        IEnumerable<Permission>? permissions = null)
    {
        var organizationId = OrganizationId.New();
        var branchId = BranchId.New();

        var userSession = new FakeCurrentUserSession();

        if (authenticated)
        {
            userSession.CurrentUser = new AuthenticatedUser(
                UserId.New(),
                organizationId,
                RoleId.New(),
                "JPEREZ",
                "Juan Pérez",
                "Gerente",
                permissions ?? [Permission.ManageProducts, Permission.AdjustInventory]);
        }

        var registerSession = new FakeCurrentRegisterSession();

        if (registerOpen)
        {
            registerSession.Current = new ActiveRegisterSession(
                RegisterSessionId.New(),
                organizationId,
                branchId,
                RegisterId.New(),
                "Caja 1",
                UserId.New(),
                "Juan Pérez",
                CreatedAtUtc,
                100m,
                "MXN");
        }

        var productRepository = new FakeProductRepository();
        var inventoryItemRepository = new FakeInventoryItemRepository();
        var inventoryMovementRepository = new FakeInventoryMovementRepository();
        var unitOfWork = new FakeUnitOfWork();
        var clock = new FakeClock(UtcNow);

        var service = new ProductManagementService(
            userSession, registerSession, productRepository, inventoryItemRepository,
            inventoryMovementRepository, unitOfWork, clock);

        return new Fixture(
            service, userSession, registerSession, productRepository, inventoryItemRepository,
            inventoryMovementRepository, unitOfWork, organizationId, branchId);
    }

    private static Product CreateProduct(
        OrganizationId organizationId, string sku = "SKU-001", string? barcode = "7501234567890",
        bool tracksInventory = true, bool isActive = true)
    {
        var product = new Product(
            ProductId.New(),
            organizationId,
            new Sku(sku),
            barcode is null ? null : new Barcode(barcode),
            "Producto de prueba",
            "Descripción original",
            new Money(10m, "MXN"),
            new Money(5m, "MXN"),
            tracksInventory,
            CreatedAtUtc);

        if (!isActive)
        {
            product.Deactivate();
        }

        return product;
    }

    private static InventoryItem CreateInventoryItem(BranchId branchId, ProductId productId, decimal quantity = 10m, decimal reorderPoint = 2m) =>
        new(InventoryItemId.New(), branchId, productId, quantity, reorderPoint, CreatedAtUtc);

    // ---------- GetByIdAsync ----------

    [Fact]
    public async Task GetByIdAsyncReturnsNullWhenNotAuthenticated()
    {
        var fixture = CreateFixture(authenticated: false);

        var result = await fixture.Service.GetByIdAsync(ProductId.New());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsyncReturnsNullWithoutManageProductsPermission()
    {
        var fixture = CreateFixture(permissions: [Permission.ProcessSale]);

        var result = await fixture.Service.GetByIdAsync(ProductId.New());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsyncReturnsNullWhenProductDoesNotExist()
    {
        var fixture = CreateFixture();

        var result = await fixture.Service.GetByIdAsync(ProductId.New());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsyncReturnsNullForAProductOfAnotherOrganization()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(OrganizationId.New());
        fixture.ProductRepository.Add(product);

        var result = await fixture.Service.GetByIdAsync(product.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsyncReturnsDetailsWithInventoryWhenTracksInventoryIsTrue()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId, tracksInventory: true);
        fixture.ProductRepository.Add(product);
        fixture.InventoryItemRepository.Add(CreateInventoryItem(fixture.BranchId, product.Id, 8m, 3m));

        var result = await fixture.Service.GetByIdAsync(product.Id);

        Assert.NotNull(result);
        Assert.Equal(product.Sku.Value, result!.Sku);
        Assert.Equal(8m, result.CurrentQuantity);
        Assert.Equal(3m, result.ReorderPoint);
    }

    [Fact]
    public async Task GetByIdAsyncReturnsZeroQuantityWhenTracksInventoryIsFalse()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId, tracksInventory: false);
        fixture.ProductRepository.Add(product);

        var result = await fixture.Service.GetByIdAsync(product.Id);

        Assert.NotNull(result);
        Assert.False(result!.TracksInventory);
        Assert.Equal(0m, result.CurrentQuantity);
    }

    // ---------- UpdateAsync ----------

    [Fact]
    public async Task UpdateAsyncFailsWhenNotAuthenticated()
    {
        var fixture = CreateFixture(authenticated: false);

        var result = await fixture.Service.UpdateAsync(
            new UpdateProductRequest(ProductId.New(), null, "Nuevo nombre", null, 12m, null, null));

        Assert.Equal(UpdateProductResultStatus.NotAuthenticated, result.Status);
    }

    [Fact]
    public async Task UpdateAsyncFailsWithoutManageProductsPermission()
    {
        var fixture = CreateFixture(permissions: [Permission.ProcessSale]);

        var result = await fixture.Service.UpdateAsync(
            new UpdateProductRequest(ProductId.New(), null, "Nuevo nombre", null, 12m, null, null));

        Assert.Equal(UpdateProductResultStatus.NotAuthorized, result.Status);
    }

    [Fact]
    public async Task UpdateAsyncFailsWhenProductDoesNotExist()
    {
        var fixture = CreateFixture();

        var result = await fixture.Service.UpdateAsync(
            new UpdateProductRequest(ProductId.New(), null, "Nuevo nombre", null, 12m, null, null));

        Assert.Equal(UpdateProductResultStatus.ProductNotFound, result.Status);
    }

    [Fact]
    public async Task UpdateAsyncFailsForAProductOfAnotherOrganization()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(OrganizationId.New());
        fixture.ProductRepository.Add(product);

        var result = await fixture.Service.UpdateAsync(
            new UpdateProductRequest(product.Id, null, "Nuevo nombre", null, 12m, null, null));

        Assert.Equal(UpdateProductResultStatus.ProductNotFound, result.Status);
    }

    [Fact]
    public async Task UpdateAsyncUpdatesNameDescriptionPriceAndCostAndCommitsOnce()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId, barcode: null, tracksInventory: false);
        fixture.ProductRepository.Add(product);

        var result = await fixture.Service.UpdateAsync(new UpdateProductRequest(
            product.Id, null, "Nombre actualizado", "Descripción actualizada", 20m, 8m, null));

        Assert.True(result.Success);
        Assert.Equal("Nombre actualizado", result.Product!.Name);
        Assert.Equal("Descripción actualizada", result.Product.Description);
        Assert.Equal(20m, result.Product.SalePriceAmount);
        Assert.Equal(8m, result.Product.CostAmount);
        Assert.Equal(1, fixture.ProductRepository.UpdateCallCount);
        Assert.Equal(1, fixture.UnitOfWork.CommitCallCount);
    }

    [Fact]
    public async Task UpdateAsyncChangesBarcode()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId, barcode: "7501234567890");
        fixture.ProductRepository.Add(product);
        fixture.InventoryItemRepository.Add(CreateInventoryItem(fixture.BranchId, product.Id));

        var result = await fixture.Service.UpdateAsync(new UpdateProductRequest(
            product.Id, "7509999999999", product.Name, product.Description, 10m, 5m, null));

        Assert.True(result.Success);
        Assert.Equal("7509999999999", result.Product!.Barcode);
    }

    [Fact]
    public async Task UpdateAsyncRejectsDuplicateBarcodeFromAnotherProduct()
    {
        var fixture = CreateFixture();
        var other = CreateProduct(fixture.OrganizationId, sku: "SKU-OTHER", barcode: "7501111111111");
        var product = CreateProduct(fixture.OrganizationId, sku: "SKU-002", barcode: "7502222222222");
        fixture.ProductRepository.Add(other);
        fixture.ProductRepository.Add(product);

        var result = await fixture.Service.UpdateAsync(new UpdateProductRequest(
            product.Id, "7501111111111", product.Name, product.Description, 10m, 5m, null));

        Assert.Equal(UpdateProductResultStatus.DuplicateBarcode, result.Status);
        Assert.Equal(0, fixture.ProductRepository.UpdateCallCount);
    }

    [Fact]
    public async Task UpdateAsyncAllowsKeepingTheSameBarcodeUnchanged()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId, barcode: "7501234567890");
        fixture.ProductRepository.Add(product);

        var result = await fixture.Service.UpdateAsync(new UpdateProductRequest(
            product.Id, "7501234567890", product.Name, product.Description, 10m, 5m, null));

        Assert.True(result.Success);
    }

    [Fact]
    public async Task UpdateAsyncRejectsNegativeSalePrice()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId);
        fixture.ProductRepository.Add(product);

        var result = await fixture.Service.UpdateAsync(new UpdateProductRequest(
            product.Id, null, product.Name, product.Description, -5m, null, null));

        Assert.Equal(UpdateProductResultStatus.InvalidSalePrice, result.Status);
        Assert.Equal(0, fixture.ProductRepository.UpdateCallCount);
    }

    [Fact]
    public async Task UpdateAsyncRejectsNegativeCost()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId);
        fixture.ProductRepository.Add(product);

        var result = await fixture.Service.UpdateAsync(new UpdateProductRequest(
            product.Id, null, product.Name, product.Description, 10m, -1m, null));

        Assert.Equal(UpdateProductResultStatus.InvalidCost, result.Status);
    }

    [Fact]
    public async Task UpdateAsyncPreservesTheExistingCurrencyRegardlessOfRegisterSessionCurrency()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId);
        fixture.ProductRepository.Add(product);

        var result = await fixture.Service.UpdateAsync(new UpdateProductRequest(
            product.Id, null, product.Name, product.Description, 15m, null, null));

        Assert.True(result.Success);
        Assert.Equal("MXN", result.Product!.Currency);
    }

    [Fact]
    public async Task UpdateAsyncDoesNotChangeSku()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId, sku: "SKU-FIXED");
        fixture.ProductRepository.Add(product);

        var result = await fixture.Service.UpdateAsync(new UpdateProductRequest(
            product.Id, null, "Otro nombre", null, 10m, null, null));

        Assert.True(result.Success);
        Assert.Equal("SKU-FIXED", result.Product!.Sku);
    }

    [Fact]
    public async Task UpdateAsyncDoesNotChangeTracksInventory()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId, tracksInventory: true);
        fixture.ProductRepository.Add(product);
        fixture.InventoryItemRepository.Add(CreateInventoryItem(fixture.BranchId, product.Id));

        var result = await fixture.Service.UpdateAsync(new UpdateProductRequest(
            product.Id, null, product.Name, product.Description, 10m, null, 4m));

        Assert.True(result.Success);
        Assert.True(result.Product!.TracksInventory);
    }

    [Fact]
    public async Task UpdateAsyncChangesReorderPointWithoutChangingQuantity()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId, tracksInventory: true);
        fixture.ProductRepository.Add(product);
        fixture.InventoryItemRepository.Add(CreateInventoryItem(fixture.BranchId, product.Id, quantity: 7m, reorderPoint: 2m));

        var result = await fixture.Service.UpdateAsync(new UpdateProductRequest(
            product.Id, null, product.Name, product.Description, 10m, null, 5m));

        Assert.True(result.Success);
        Assert.Equal(5m, result.Product!.ReorderPoint);
        Assert.Equal(7m, result.Product.CurrentQuantity);
        Assert.Equal(1, fixture.InventoryItemRepository.UpdateCallCount);
        Assert.Equal(0, fixture.InventoryItemRepository.AddCallCount);
    }

    [Fact]
    public async Task UpdateAsyncIgnoresReorderPointWhenProductDoesNotTrackInventory()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId, tracksInventory: false);
        fixture.ProductRepository.Add(product);

        var result = await fixture.Service.UpdateAsync(new UpdateProductRequest(
            product.Id, null, product.Name, product.Description, 10m, null, 5m));

        Assert.True(result.Success);
        Assert.Equal(0, fixture.InventoryItemRepository.UpdateCallCount);
        Assert.Equal(0, fixture.InventoryItemRepository.AddCallCount);
    }

    [Fact]
    public async Task UpdateAsyncCommitsExactlyOnceEvenWhenReorderPointChanges()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId, tracksInventory: true);
        fixture.ProductRepository.Add(product);
        fixture.InventoryItemRepository.Add(CreateInventoryItem(fixture.BranchId, product.Id));

        await fixture.Service.UpdateAsync(new UpdateProductRequest(
            product.Id, null, product.Name, product.Description, 10m, null, 6m));

        Assert.Equal(1, fixture.UnitOfWork.CommitCallCount);
    }

    // ---------- SetActiveAsync ----------

    [Fact]
    public async Task SetActiveAsyncDeactivatesAProduct()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId, isActive: true);
        fixture.ProductRepository.Add(product);

        var result = await fixture.Service.SetActiveAsync(product.Id, false);

        Assert.True(result.Success);
        Assert.False(result.Product!.IsActive);
        Assert.Equal(1, fixture.ProductRepository.UpdateCallCount);
        Assert.Equal(1, fixture.UnitOfWork.CommitCallCount);
    }

    [Fact]
    public async Task SetActiveAsyncReactivatesAProduct()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId, isActive: false);
        fixture.ProductRepository.Add(product);

        var result = await fixture.Service.SetActiveAsync(product.Id, true);

        Assert.True(result.Success);
        Assert.True(result.Product!.IsActive);
    }

    [Fact]
    public async Task SetActiveAsyncDoesNotTouchInventory()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId, tracksInventory: true);
        fixture.ProductRepository.Add(product);
        fixture.InventoryItemRepository.Add(CreateInventoryItem(fixture.BranchId, product.Id, quantity: 9m));

        var result = await fixture.Service.SetActiveAsync(product.Id, false);

        Assert.True(result.Success);
        Assert.Equal(9m, result.Product!.CurrentQuantity);
        Assert.Equal(0, fixture.InventoryItemRepository.UpdateCallCount);
    }

    [Fact]
    public async Task SetActiveAsyncFailsWithoutManageProductsPermission()
    {
        var fixture = CreateFixture(permissions: [Permission.ProcessSale]);
        var result = await fixture.Service.SetActiveAsync(ProductId.New(), false);

        Assert.Equal(UpdateProductResultStatus.NotAuthorized, result.Status);
    }

    // ---------- AdjustInventoryAsync ----------

    [Fact]
    public async Task AdjustInventoryAsyncIncreasesQuantityAndCreatesAManualIncreaseMovement()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId, tracksInventory: true);
        fixture.ProductRepository.Add(product);
        fixture.InventoryItemRepository.Add(CreateInventoryItem(fixture.BranchId, product.Id, quantity: 10m));

        var result = await fixture.Service.AdjustInventoryAsync(
            new AdjustProductInventoryRequest(product.Id, InventoryAdjustmentType.Increase, 5m));

        Assert.True(result.Success);
        Assert.Equal(15m, result.NewQuantity);
        Assert.Equal(1, fixture.InventoryMovementRepository.AddCallCount);
        Assert.Equal(InventoryMovementType.ManualIncrease, fixture.InventoryMovementRepository.AddedMovements[0].Type);
        Assert.Equal(1, fixture.InventoryItemRepository.UpdateCallCount);
        Assert.Equal(1, fixture.UnitOfWork.CommitCallCount);
    }

    [Fact]
    public async Task AdjustInventoryAsyncDecreasesQuantityAndCreatesAManualDecreaseMovement()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId, tracksInventory: true);
        fixture.ProductRepository.Add(product);
        fixture.InventoryItemRepository.Add(CreateInventoryItem(fixture.BranchId, product.Id, quantity: 10m));

        var result = await fixture.Service.AdjustInventoryAsync(
            new AdjustProductInventoryRequest(product.Id, InventoryAdjustmentType.Decrease, 4m));

        Assert.True(result.Success);
        Assert.Equal(6m, result.NewQuantity);
        Assert.Equal(InventoryMovementType.ManualDecrease, fixture.InventoryMovementRepository.AddedMovements[0].Type);
    }

    [Fact]
    public async Task AdjustInventoryAsyncAllowsDecreasingToExactlyZero()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId, tracksInventory: true);
        fixture.ProductRepository.Add(product);
        fixture.InventoryItemRepository.Add(CreateInventoryItem(fixture.BranchId, product.Id, quantity: 4m));

        var result = await fixture.Service.AdjustInventoryAsync(
            new AdjustProductInventoryRequest(product.Id, InventoryAdjustmentType.Decrease, 4m));

        Assert.True(result.Success);
        Assert.Equal(0m, result.NewQuantity);
    }

    [Fact]
    public async Task AdjustInventoryAsyncRejectsADecreaseThatWouldGoNegative()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId, tracksInventory: true);
        fixture.ProductRepository.Add(product);
        fixture.InventoryItemRepository.Add(CreateInventoryItem(fixture.BranchId, product.Id, quantity: 3m));

        var result = await fixture.Service.AdjustInventoryAsync(
            new AdjustProductInventoryRequest(product.Id, InventoryAdjustmentType.Decrease, 4m));

        Assert.Equal(AdjustProductInventoryResultStatus.ResultingQuantityNegative, result.Status);
        Assert.Equal(0, fixture.InventoryMovementRepository.AddCallCount);
        Assert.Equal(0, fixture.InventoryItemRepository.UpdateCallCount);
        Assert.Equal(0, fixture.UnitOfWork.CommitCallCount);
    }

    [Fact]
    public async Task AdjustInventoryAsyncRejectsZeroOrNegativeQuantity()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId, tracksInventory: true);
        fixture.ProductRepository.Add(product);
        fixture.InventoryItemRepository.Add(CreateInventoryItem(fixture.BranchId, product.Id));

        var result = await fixture.Service.AdjustInventoryAsync(
            new AdjustProductInventoryRequest(product.Id, InventoryAdjustmentType.Increase, 0m));

        Assert.Equal(AdjustProductInventoryResultStatus.InvalidQuantity, result.Status);
    }

    [Fact]
    public async Task AdjustInventoryAsyncFailsWhenProductDoesNotTrackInventory()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId, tracksInventory: false);
        fixture.ProductRepository.Add(product);

        var result = await fixture.Service.AdjustInventoryAsync(
            new AdjustProductInventoryRequest(product.Id, InventoryAdjustmentType.Increase, 5m));

        Assert.Equal(AdjustProductInventoryResultStatus.ProductDoesNotTrackInventory, result.Status);
    }

    [Fact]
    public async Task AdjustInventoryAsyncFailsWhenNoInventoryItemExistsForTheBranch()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId, tracksInventory: true);
        fixture.ProductRepository.Add(product);

        var result = await fixture.Service.AdjustInventoryAsync(
            new AdjustProductInventoryRequest(product.Id, InventoryAdjustmentType.Increase, 5m));

        Assert.Equal(AdjustProductInventoryResultStatus.InventoryItemNotFound, result.Status);
    }

    [Fact]
    public async Task AdjustInventoryAsyncFailsWithoutAdjustInventoryPermission()
    {
        var fixture = CreateFixture(permissions: [Permission.ManageProducts]);
        var product = CreateProduct(fixture.OrganizationId, tracksInventory: true);
        fixture.ProductRepository.Add(product);
        fixture.InventoryItemRepository.Add(CreateInventoryItem(fixture.BranchId, product.Id));

        var result = await fixture.Service.AdjustInventoryAsync(
            new AdjustProductInventoryRequest(product.Id, InventoryAdjustmentType.Increase, 5m));

        Assert.Equal(AdjustProductInventoryResultStatus.NotAuthorized, result.Status);
        Assert.Equal(0, fixture.InventoryMovementRepository.AddCallCount);
    }

    [Fact]
    public async Task AdjustInventoryAsyncFailsWithoutAnOpenRegisterSession()
    {
        var fixture = CreateFixture(registerOpen: false);

        var result = await fixture.Service.AdjustInventoryAsync(
            new AdjustProductInventoryRequest(ProductId.New(), InventoryAdjustmentType.Increase, 5m));

        Assert.Equal(AdjustProductInventoryResultStatus.RegisterSessionRequired, result.Status);
    }

    [Fact]
    public async Task AdjustInventoryAsyncFailsForAProductOfAnotherOrganization()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(OrganizationId.New(), tracksInventory: true);
        fixture.ProductRepository.Add(product);

        var result = await fixture.Service.AdjustInventoryAsync(
            new AdjustProductInventoryRequest(product.Id, InventoryAdjustmentType.Increase, 5m));

        Assert.Equal(AdjustProductInventoryResultStatus.ProductNotFound, result.Status);
    }

    [Fact]
    public async Task AdjustInventoryAsyncUpdatesTheInventoryItemAndCommitsExactlyOnce()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId, tracksInventory: true);
        fixture.ProductRepository.Add(product);
        fixture.InventoryItemRepository.Add(CreateInventoryItem(fixture.BranchId, product.Id, quantity: 10m));

        await fixture.Service.AdjustInventoryAsync(
            new AdjustProductInventoryRequest(product.Id, InventoryAdjustmentType.Increase, 5m));

        Assert.Equal(1, fixture.InventoryItemRepository.UpdateCallCount);
        Assert.Equal(1, fixture.InventoryMovementRepository.AddCallCount);
        Assert.Equal(1, fixture.UnitOfWork.CommitCallCount);

        var reloaded = await fixture.InventoryItemRepository.GetByBranchAndProductAsync(
            fixture.BranchId, product.Id, CancellationToken.None);
        Assert.Equal(15m, reloaded!.Quantity);
    }
}
