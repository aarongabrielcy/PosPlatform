using Pos.Application.Authentication;
using Pos.Application.Enforcement;
using Pos.Application.Products.ManageProduct;
using Pos.Application.RegisterSessions;
using Pos.Application.Tests.Common.Time;
using Pos.Application.Tests.Enforcement;
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
        FakeProductCatalogQuery ProductCatalogQuery,
        FakeProductAuditRepository ProductAuditRepository,
        FakeProductAuditQuery ProductAuditQuery,
        FakeAdministrativeNotificationWriter AdministrativeNotificationWriter,
        FakeInstallationEnforcementStateService EnforcementStateService,
        FakeUnitOfWork UnitOfWork,
        OrganizationId OrganizationId,
        BranchId BranchId);

    private static Fixture CreateFixture(
        bool authenticated = true,
        bool registerOpen = true,
        IEnumerable<Permission>? permissions = null,
        FakeProductAuditQuery? productAuditQuery = null,
        FakeProductCatalogQuery? productCatalogQuery = null,
        InstallationEnforcementState enforcementState = InstallationEnforcementState.Allowed)
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
        productCatalogQuery ??= new FakeProductCatalogQuery();
        var productAuditRepository = new FakeProductAuditRepository();
        productAuditQuery ??= new FakeProductAuditQuery();
        var administrativeNotificationWriter = new FakeAdministrativeNotificationWriter();
        var enforcementStateService = new FakeInstallationEnforcementStateService(enforcementState);
        var unitOfWork = new FakeUnitOfWork();
        var clock = new FakeClock(UtcNow);

        var service = new ProductManagementService(
            userSession, registerSession, productRepository, inventoryItemRepository,
            inventoryMovementRepository, productCatalogQuery, productAuditRepository, productAuditQuery,
            administrativeNotificationWriter, enforcementStateService, unitOfWork, clock);

        return new Fixture(
            service, userSession, registerSession, productRepository, inventoryItemRepository,
            inventoryMovementRepository, productCatalogQuery, productAuditRepository, productAuditQuery,
            administrativeNotificationWriter, enforcementStateService, unitOfWork, organizationId, branchId);
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

    // ---------- Guarda de enforcement (secciones 19/20/32 de la tarea) ----------

    [Theory]
    [InlineData(InstallationEnforcementState.Suspended)]
    [InlineData(InstallationEnforcementState.CredentialInvalid)]
    [InlineData(InstallationEnforcementState.Decommissioned)]
    public async Task UpdateAsyncWhileInstallationIsRestrictedReturnsInstallationRestrictedAndPersistsNothing(
        InstallationEnforcementState restrictedState)
    {
        var fixture = CreateFixture();
        fixture.EnforcementStateService.SetCurrentForTest(restrictedState);

        var result = await fixture.Service.UpdateAsync(
            new UpdateProductRequest(ProductId.New(), "SKU-001", null, "Nuevo nombre", null, 12m, null, null));

        Assert.Equal(UpdateProductResultStatus.InstallationRestricted, result.Status);
        Assert.Equal(0, fixture.UnitOfWork.CommitCallCount);
    }

    // ---------- UpdateAsync ----------

    [Fact]
    public async Task UpdateAsyncFailsWhenNotAuthenticated()
    {
        var fixture = CreateFixture(authenticated: false);

        var result = await fixture.Service.UpdateAsync(
            new UpdateProductRequest(ProductId.New(), "SKU-001", null, "Nuevo nombre", null, 12m, null, null));

        Assert.Equal(UpdateProductResultStatus.NotAuthenticated, result.Status);
    }

    [Fact]
    public async Task UpdateAsyncFailsWithoutManageProductsPermission()
    {
        var fixture = CreateFixture(permissions: [Permission.ProcessSale]);

        var result = await fixture.Service.UpdateAsync(
            new UpdateProductRequest(ProductId.New(), "SKU-001", null, "Nuevo nombre", null, 12m, null, null));

        Assert.Equal(UpdateProductResultStatus.NotAuthorized, result.Status);
    }

    [Fact]
    public async Task UpdateAsyncFailsWhenProductDoesNotExist()
    {
        var fixture = CreateFixture();

        var result = await fixture.Service.UpdateAsync(
            new UpdateProductRequest(ProductId.New(), "SKU-001", null, "Nuevo nombre", null, 12m, null, null));

        Assert.Equal(UpdateProductResultStatus.ProductNotFound, result.Status);
    }

    [Fact]
    public async Task UpdateAsyncFailsForAProductOfAnotherOrganization()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(OrganizationId.New());
        fixture.ProductRepository.Add(product);

        var result = await fixture.Service.UpdateAsync(
            new UpdateProductRequest(product.Id, product.Sku.Value, null, "Nuevo nombre", null, 12m, null, null));

        Assert.Equal(UpdateProductResultStatus.ProductNotFound, result.Status);
    }

    [Fact]
    public async Task UpdateAsyncUpdatesNameDescriptionPriceAndCostAndCommitsOnce()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId, barcode: null, tracksInventory: false);
        fixture.ProductRepository.Add(product);

        var result = await fixture.Service.UpdateAsync(new UpdateProductRequest(
            product.Id, product.Sku.Value, null, "Nombre actualizado", "Descripción actualizada", 20m, 8m, null));

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
            product.Id, product.Sku.Value, "7509999999999", product.Name, product.Description, 10m, 5m, null));

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
            product.Id, product.Sku.Value, "7501111111111", product.Name, product.Description, 10m, 5m, null));

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
            product.Id, product.Sku.Value, "7501234567890", product.Name, product.Description, 10m, 5m, null));

        Assert.True(result.Success);
    }

    [Fact]
    public async Task UpdateAsyncRejectsNegativeSalePrice()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId);
        fixture.ProductRepository.Add(product);

        var result = await fixture.Service.UpdateAsync(new UpdateProductRequest(
            product.Id, product.Sku.Value, null, product.Name, product.Description, -5m, null, null));

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
            product.Id, product.Sku.Value, null, product.Name, product.Description, 10m, -1m, null));

        Assert.Equal(UpdateProductResultStatus.InvalidCost, result.Status);
    }

    [Fact]
    public async Task UpdateAsyncPreservesTheExistingCurrencyRegardlessOfRegisterSessionCurrency()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId);
        fixture.ProductRepository.Add(product);

        var result = await fixture.Service.UpdateAsync(new UpdateProductRequest(
            product.Id, product.Sku.Value, null, product.Name, product.Description, 15m, null, null));

        Assert.True(result.Success);
        Assert.Equal("MXN", result.Product!.Currency);
    }

    [Fact]
    public async Task UpdateAsyncKeepsTheSameSkuWhenUnchanged()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId, sku: "SKU-FIXED");
        fixture.ProductRepository.Add(product);

        var result = await fixture.Service.UpdateAsync(new UpdateProductRequest(
            product.Id, "SKU-FIXED", null, "Otro nombre", null, 10m, null, null));

        Assert.True(result.Success);
        Assert.Equal("SKU-FIXED", result.Product!.Sku);
        Assert.Equal(0, fixture.ProductRepository.GetBySkuCallCount);
    }

    [Fact]
    public async Task UpdateAsyncChangesTheSkuAndNormalizesIt()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId, sku: "SKU-OLD");
        fixture.ProductRepository.Add(product);
        var originalProductId = product.Id;

        var result = await fixture.Service.UpdateAsync(new UpdateProductRequest(
            product.Id, " sku-new ", null, product.Name, product.Description, 10m, null, null));

        Assert.True(result.Success);
        Assert.Equal("SKU-NEW", result.Product!.Sku);
        Assert.Equal(originalProductId, result.Product.ProductId);
    }

    [Fact]
    public async Task UpdateAsyncRejectsAnInvalidSku()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId, sku: "SKU-OLD");
        fixture.ProductRepository.Add(product);

        var result = await fixture.Service.UpdateAsync(new UpdateProductRequest(
            product.Id, "!", null, product.Name, product.Description, 10m, null, null));

        Assert.Equal(UpdateProductResultStatus.InvalidSku, result.Status);
        Assert.Equal(0, fixture.ProductRepository.UpdateCallCount);
    }

    [Fact]
    public async Task UpdateAsyncRejectsDuplicateSkuFromAnotherProductInTheSameOrganization()
    {
        var fixture = CreateFixture();
        var other = CreateProduct(fixture.OrganizationId, sku: "SKU-TAKEN", barcode: "7501111111111");
        var product = CreateProduct(fixture.OrganizationId, sku: "SKU-OWN", barcode: "7502222222222");
        fixture.ProductRepository.Add(other);
        fixture.ProductRepository.Add(product);

        var result = await fixture.Service.UpdateAsync(new UpdateProductRequest(
            product.Id, "SKU-TAKEN", null, product.Name, product.Description, 10m, null, null));

        Assert.Equal(UpdateProductResultStatus.DuplicateSku, result.Status);
        Assert.Equal(0, fixture.ProductRepository.UpdateCallCount);
    }

    [Fact]
    public async Task UpdateAsyncAllowsTheSameSkuUsedByAProductInAnotherOrganization()
    {
        var fixture = CreateFixture();
        var otherOrganizationProduct = CreateProduct(OrganizationId.New(), sku: "SKU-SHARED");
        var product = CreateProduct(fixture.OrganizationId, sku: "SKU-OWN");
        fixture.ProductRepository.Add(otherOrganizationProduct);
        fixture.ProductRepository.Add(product);

        var result = await fixture.Service.UpdateAsync(new UpdateProductRequest(
            product.Id, "SKU-SHARED", null, product.Name, product.Description, 10m, null, null));

        Assert.True(result.Success);
        Assert.Equal("SKU-SHARED", result.Product!.Sku);
    }

    [Fact]
    public async Task UpdateAsyncDoesNotChangeTracksInventory()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId, tracksInventory: true);
        fixture.ProductRepository.Add(product);
        fixture.InventoryItemRepository.Add(CreateInventoryItem(fixture.BranchId, product.Id));

        var result = await fixture.Service.UpdateAsync(new UpdateProductRequest(
            product.Id, product.Sku.Value, null, product.Name, product.Description, 10m, null, 4m));

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
            product.Id, product.Sku.Value, null, product.Name, product.Description, 10m, null, 5m));

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
            product.Id, product.Sku.Value, null, product.Name, product.Description, 10m, null, 5m));

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
            product.Id, product.Sku.Value, null, product.Name, product.Description, 10m, null, 6m));

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

    [Theory]
    [InlineData(InstallationEnforcementState.Suspended)]
    [InlineData(InstallationEnforcementState.CredentialInvalid)]
    [InlineData(InstallationEnforcementState.Decommissioned)]
    public async Task SetActiveAsyncWhileInstallationIsRestrictedReturnsInstallationRestrictedAndPersistsNothing(
        InstallationEnforcementState restrictedState)
    {
        var fixture = CreateFixture();
        fixture.EnforcementStateService.SetCurrentForTest(restrictedState);

        var result = await fixture.Service.SetActiveAsync(ProductId.New(), false);

        Assert.Equal(UpdateProductResultStatus.InstallationRestricted, result.Status);
        Assert.Equal(0, fixture.UnitOfWork.CommitCallCount);
    }

    // ---------- AdjustInventoryAsync ----------

    [Theory]
    [InlineData(InstallationEnforcementState.Suspended)]
    [InlineData(InstallationEnforcementState.CredentialInvalid)]
    [InlineData(InstallationEnforcementState.Decommissioned)]
    public async Task AdjustInventoryAsyncWhileInstallationIsRestrictedReturnsInstallationRestrictedAndPersistsNothing(
        InstallationEnforcementState restrictedState)
    {
        var fixture = CreateFixture();
        fixture.EnforcementStateService.SetCurrentForTest(restrictedState);

        var result = await fixture.Service.AdjustInventoryAsync(
            new AdjustProductInventoryRequest(ProductId.New(), InventoryAdjustmentType.Increase, 1m));

        Assert.Equal(AdjustProductInventoryResultStatus.InstallationRestricted, result.Status);
        Assert.Equal(0, fixture.UnitOfWork.CommitCallCount);
        Assert.Equal(0, fixture.InventoryMovementRepository.AddCallCount);
    }

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

    // TAREA 25A-FIX sección 12: incrementar desde una existencia agotada (0) sigue siendo un
    // incremento normal.
    [Fact]
    public async Task AdjustInventoryAsyncAllowsIncreasingFromZero()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId, tracksInventory: true);
        fixture.ProductRepository.Add(product);
        fixture.InventoryItemRepository.Add(CreateInventoryItem(fixture.BranchId, product.Id, quantity: 0m));

        var result = await fixture.Service.AdjustInventoryAsync(
            new AdjustProductInventoryRequest(product.Id, InventoryAdjustmentType.Increase, 5m));

        Assert.True(result.Success);
        Assert.Equal(5m, result.NewQuantity);
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

    // ---------- GetCatalogPageAsync ----------

    [Fact]
    public async Task GetCatalogPageAsyncReturnsEmptyWhenNotAuthenticated()
    {
        var fixture = CreateFixture(authenticated: false);

        var result = await fixture.Service.GetCatalogPageAsync(null, ProductCatalogStatusFilter.All, 0, 50);

        Assert.Empty(result.Items);
        Assert.Equal(0, fixture.ProductCatalogQuery.SearchPageCallCount);
    }

    [Fact]
    public async Task GetCatalogPageAsyncReturnsEmptyWithoutManageProductsPermission()
    {
        var fixture = CreateFixture(permissions: [Permission.ProcessSale]);

        var result = await fixture.Service.GetCatalogPageAsync(null, ProductCatalogStatusFilter.All, 0, 50);

        Assert.Empty(result.Items);
        Assert.Equal(0, fixture.ProductCatalogQuery.SearchPageCallCount);
    }

    [Fact]
    public async Task GetCatalogPageAsyncReturnsEmptyWithoutAnOpenRegisterSession()
    {
        var fixture = CreateFixture(registerOpen: false);

        var result = await fixture.Service.GetCatalogPageAsync(null, ProductCatalogStatusFilter.All, 0, 50);

        Assert.Empty(result.Items);
        Assert.Equal(0, fixture.ProductCatalogQuery.SearchPageCallCount);
    }

    [Fact]
    public async Task GetCatalogPageAsyncDelegatesToTheCatalogQueryWithTheCurrentOrganizationAndBranch()
    {
        var fixture = CreateFixture();

        await fixture.Service.GetCatalogPageAsync("agua", ProductCatalogStatusFilter.LowStock, 50, 25);

        Assert.Equal(1, fixture.ProductCatalogQuery.SearchPageCallCount);
        Assert.Equal(fixture.OrganizationId, fixture.ProductCatalogQuery.LastOrganizationId);
        Assert.Equal(fixture.BranchId, fixture.ProductCatalogQuery.LastBranchId);
        Assert.Equal("agua", fixture.ProductCatalogQuery.LastSearchTerm);
        Assert.Equal(ProductCatalogStatusFilter.LowStock, fixture.ProductCatalogQuery.LastFilter);
        Assert.Equal(50, fixture.ProductCatalogQuery.LastSkip);
        Assert.Equal(25, fixture.ProductCatalogQuery.LastTake);
    }

    // ---------- GetDashboardSummaryAsync ----------

    [Fact]
    public async Task GetDashboardSummaryAsyncReturnsEmptyWhenNotAuthenticated()
    {
        var fixture = CreateFixture(authenticated: false);

        var result = await fixture.Service.GetDashboardSummaryAsync();

        Assert.Equal(ProductCatalogSummary.Empty, result);
        Assert.Equal(0, fixture.ProductCatalogQuery.GetSummaryCallCount);
    }

    [Fact]
    public async Task GetDashboardSummaryAsyncReturnsEmptyWithoutManageProductsPermission()
    {
        var fixture = CreateFixture(permissions: [Permission.ProcessSale]);

        var result = await fixture.Service.GetDashboardSummaryAsync();

        Assert.Equal(ProductCatalogSummary.Empty, result);
        Assert.Equal(0, fixture.ProductCatalogQuery.GetSummaryCallCount);
    }

    [Fact]
    public async Task GetDashboardSummaryAsyncReturnsEmptyWithoutAnOpenRegisterSession()
    {
        var fixture = CreateFixture(registerOpen: false);

        var result = await fixture.Service.GetDashboardSummaryAsync();

        Assert.Equal(ProductCatalogSummary.Empty, result);
    }

    [Fact]
    public async Task GetDashboardSummaryAsyncDelegatesToTheCatalogQueryWithTheCurrentOrganizationAndBranch()
    {
        var fixture = CreateFixture();

        var result = await fixture.Service.GetDashboardSummaryAsync();

        Assert.Equal(1, fixture.ProductCatalogQuery.GetSummaryCallCount);
        Assert.Equal(fixture.OrganizationId, fixture.ProductCatalogQuery.LastOrganizationId);
        Assert.Equal(fixture.BranchId, fixture.ProductCatalogQuery.LastBranchId);
    }

    // ---------- Product audit (TAREA 24D) ----------

    [Fact]
    public async Task UpdateAsyncCreatesASingleUpdatedAuditEventWithOneChangePerModifiedField()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId, sku: "SKU-EDIT");
        fixture.ProductRepository.Add(product);

        var result = await fixture.Service.UpdateAsync(new UpdateProductRequest(
            product.Id, product.Sku.Value, null, "Agua Natural", product.Description, 27.50m, null, null));

        Assert.True(result.Success);
        Assert.Equal(1, fixture.ProductAuditRepository.AddCallCount);

        var auditEvent = Assert.Single(fixture.ProductAuditRepository.AddedEvents);
        Assert.Equal(Pos.Domain.ProductAudit.ProductAuditAction.Updated, auditEvent.Action);

        var nameChange = Assert.Single(
            auditEvent.Changes, change => change.FieldName == Pos.Domain.ProductAudit.ProductAuditField.Name);
        Assert.Equal("Producto de prueba", nameChange.OldValue);
        Assert.Equal("Agua Natural", nameChange.NewValue);

        var priceChange = Assert.Single(
            auditEvent.Changes, change => change.FieldName == Pos.Domain.ProductAudit.ProductAuditField.SalePrice);
        Assert.Equal("MXN 10.00", priceChange.OldValue);
        Assert.Equal("MXN 27.50", priceChange.NewValue);
    }

    [Fact]
    public async Task UpdateAsyncDoesNotCreateAnAuditEventWhenNothingActuallyChanged()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId, sku: "SKU-NOCHANGE");
        fixture.ProductRepository.Add(product);

        var result = await fixture.Service.UpdateAsync(new UpdateProductRequest(
            product.Id, product.Sku.Value, product.Barcode?.Value, product.Name, product.Description,
            product.SalePrice.Amount, product.Cost?.Amount, null));

        Assert.True(result.Success);
        Assert.Equal(0, fixture.ProductAuditRepository.AddCallCount);
    }

    [Fact]
    public async Task UpdateAsyncIncludesReorderPointChangeInTheSameAuditEvent()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId, tracksInventory: true);
        fixture.ProductRepository.Add(product);
        fixture.InventoryItemRepository.Add(CreateInventoryItem(fixture.BranchId, product.Id, reorderPoint: 2m));

        await fixture.Service.UpdateAsync(new UpdateProductRequest(
            product.Id, product.Sku.Value, null, product.Name, product.Description, 10m, null, 6m));

        Assert.Equal(1, fixture.ProductAuditRepository.AddCallCount);
        var auditEvent = Assert.Single(fixture.ProductAuditRepository.AddedEvents);
        var reorderChange = Assert.Single(
            auditEvent.Changes, change => change.FieldName == Pos.Domain.ProductAudit.ProductAuditField.ReorderPoint);
        Assert.Equal("2", reorderChange.OldValue);
        Assert.Equal("6", reorderChange.NewValue);
    }

    [Fact]
    public async Task SetActiveAsyncCreatesADeactivatedAuditEvent()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId, isActive: true);
        fixture.ProductRepository.Add(product);

        await fixture.Service.SetActiveAsync(product.Id, false);

        Assert.Equal(1, fixture.ProductAuditRepository.AddCallCount);
        var auditEvent = Assert.Single(fixture.ProductAuditRepository.AddedEvents);
        Assert.Equal(Pos.Domain.ProductAudit.ProductAuditAction.Deactivated, auditEvent.Action);
    }

    [Fact]
    public async Task SetActiveAsyncCreatesAnActivatedAuditEvent()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId, isActive: false);
        fixture.ProductRepository.Add(product);

        await fixture.Service.SetActiveAsync(product.Id, true);

        var auditEvent = Assert.Single(fixture.ProductAuditRepository.AddedEvents);
        Assert.Equal(Pos.Domain.ProductAudit.ProductAuditAction.Activated, auditEvent.Action);
    }

    [Fact]
    public async Task SetActiveAsyncDoesNotCreateAnAuditEventWithoutARealTransition()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId, isActive: true);
        fixture.ProductRepository.Add(product);

        var result = await fixture.Service.SetActiveAsync(product.Id, true);

        Assert.True(result.Success);
        Assert.Equal(0, fixture.ProductAuditRepository.AddCallCount);
    }

    [Fact]
    public async Task AdjustInventoryAsyncCreatesAnInventoryAdjustedAuditEventInTheSameCommit()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId, tracksInventory: true);
        fixture.ProductRepository.Add(product);
        fixture.InventoryItemRepository.Add(CreateInventoryItem(fixture.BranchId, product.Id, quantity: 2m));

        await fixture.Service.AdjustInventoryAsync(
            new AdjustProductInventoryRequest(product.Id, InventoryAdjustmentType.Decrease, 2m));

        Assert.Equal(1, fixture.ProductAuditRepository.AddCallCount);
        Assert.Equal(1, fixture.UnitOfWork.CommitCallCount);

        var auditEvent = Assert.Single(fixture.ProductAuditRepository.AddedEvents);
        Assert.Equal(Pos.Domain.ProductAudit.ProductAuditAction.InventoryAdjusted, auditEvent.Action);
        var change = Assert.Single(auditEvent.Changes);
        Assert.Equal(Pos.Domain.ProductAudit.ProductAuditField.InventoryQuantity, change.FieldName);
        Assert.Equal("2", change.OldValue);
        Assert.Equal("0", change.NewValue);
    }

    // ---------- Administrative notifications (TAREA 24E) ----------

    [Fact]
    public async Task UpdateAsyncInvokesTheNotificationWriterWhenSalePriceChanges()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId, sku: "SKU-PRICE");
        fixture.ProductRepository.Add(product);

        await fixture.Service.UpdateAsync(new UpdateProductRequest(
            product.Id, product.Sku.Value, product.Barcode?.Value, product.Name, product.Description,
            27.50m, product.Cost?.Amount, null));

        Assert.Equal(1, fixture.AdministrativeNotificationWriter.CallCount);
        var auditEvent = Assert.Single(fixture.ProductAuditRepository.AddedEvents);
        Assert.Same(auditEvent, Assert.Single(fixture.AdministrativeNotificationWriter.AuditEvents));
    }

    // El servicio nunca decide "esto notifica o no" por sí mismo (TAREA 24E, sección 33): invoca
    // al writer siempre que se creó un AuditEvent, incluso para campos no sensibles como Name; la
    // política ("¿notifica?") vive únicamente en ProductAuditNotificationPolicy, dentro del writer.
    [Fact]
    public async Task UpdateAsyncStillInvokesTheWriterWhenOnlyNameChangesAndLetsItApplyThePolicy()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId, sku: "SKU-NAME");
        fixture.ProductRepository.Add(product);

        await fixture.Service.UpdateAsync(new UpdateProductRequest(
            product.Id, product.Sku.Value, product.Barcode?.Value, "Agua Natural", product.Description,
            product.SalePrice.Amount, product.Cost?.Amount, null));

        Assert.Equal(1, fixture.AdministrativeNotificationWriter.CallCount);
    }

    [Fact]
    public async Task UpdateAsyncDoesNotInvokeTheNotificationWriterWhenNothingChanged()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId, sku: "SKU-NOOP");
        fixture.ProductRepository.Add(product);

        await fixture.Service.UpdateAsync(new UpdateProductRequest(
            product.Id, product.Sku.Value, product.Barcode?.Value, product.Name, product.Description,
            product.SalePrice.Amount, product.Cost?.Amount, null));

        Assert.Equal(0, fixture.AdministrativeNotificationWriter.CallCount);
    }

    [Fact]
    public async Task SetActiveAsyncInvokesTheNotificationWriterOnARealTransition()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId, isActive: true);
        fixture.ProductRepository.Add(product);

        await fixture.Service.SetActiveAsync(product.Id, false);

        Assert.Equal(1, fixture.AdministrativeNotificationWriter.CallCount);
    }

    [Fact]
    public async Task SetActiveAsyncDoesNotInvokeTheNotificationWriterWithoutARealTransition()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId, isActive: true);
        fixture.ProductRepository.Add(product);

        await fixture.Service.SetActiveAsync(product.Id, true);

        Assert.Equal(0, fixture.AdministrativeNotificationWriter.CallCount);
    }

    [Fact]
    public async Task AdjustInventoryAsyncInvokesTheNotificationWriter()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId, tracksInventory: true);
        fixture.ProductRepository.Add(product);
        fixture.InventoryItemRepository.Add(CreateInventoryItem(fixture.BranchId, product.Id, quantity: 10m));

        await fixture.Service.AdjustInventoryAsync(
            new AdjustProductInventoryRequest(product.Id, InventoryAdjustmentType.Decrease, 3m));

        Assert.Equal(1, fixture.AdministrativeNotificationWriter.CallCount);
    }

    // TAREA 24E, sección 11/43: el writer se invoca ANTES del único CommitAsync del servicio, para
    // que Product/Audit/Notification/Recipients queden en la misma transacción.
    [Fact]
    public async Task AdjustInventoryAsyncInvokesTheNotificationWriterBeforeTheSingleCommit()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId, tracksInventory: true);
        fixture.ProductRepository.Add(product);
        fixture.InventoryItemRepository.Add(CreateInventoryItem(fixture.BranchId, product.Id, quantity: 10m));

        var commitCallCountDuringWriterCall = -1;
        fixture.AdministrativeNotificationWriter.OnCall = () =>
            commitCallCountDuringWriterCall = fixture.UnitOfWork.CommitCallCount;

        await fixture.Service.AdjustInventoryAsync(
            new AdjustProductInventoryRequest(product.Id, InventoryAdjustmentType.Decrease, 3m));

        Assert.Equal(0, commitCallCountDuringWriterCall);
        Assert.Equal(1, fixture.UnitOfWork.CommitCallCount);
    }

    [Fact]
    public async Task UpdateAsyncWithOnlyNonSensitiveChangesStillCommitsExactlyOnce()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId, sku: "SKU-ONECOMMIT");
        fixture.ProductRepository.Add(product);

        var result = await fixture.Service.UpdateAsync(new UpdateProductRequest(
            product.Id, product.Sku.Value, product.Barcode?.Value, "Agua Natural", product.Description,
            product.SalePrice.Amount, product.Cost?.Amount, null));

        Assert.True(result.Success);
        Assert.Equal(1, fixture.ProductAuditRepository.AddCallCount);
        Assert.Equal(1, fixture.UnitOfWork.CommitCallCount);
    }

    [Fact]
    public async Task GetCatalogPageAsyncMergesRecentActivityWhenUserHasViewProductAuditPermission()
    {
        var productId = ProductId.New();
        var catalogItem = new ProductCatalogItem(
            productId, "SKU-001", null, "Agua 1L", 10m, "MXN", false, 0m, 0m, true);
        var catalogQuery = new FakeProductCatalogQuery(new ProductCatalogPageResult([catalogItem], false));

        var recentActivity = new Pos.Application.ProductAudit.ProductRecentActivity(
            productId,
            Pos.Domain.Common.Identifiers.ProductAuditEventId.New(),
            Pos.Domain.ProductAudit.ProductAuditAction.Updated,
            UtcNow,
            "Cajero 02",
            [],
            1);

        var auditQuery = new FakeProductAuditQuery(
            new Dictionary<ProductId, Pos.Application.ProductAudit.ProductRecentActivity> { [productId] = recentActivity });

        var fixture = CreateFixture(
            permissions: [Permission.ManageProducts, Permission.ViewProductAudit],
            productAuditQuery: auditQuery,
            productCatalogQuery: catalogQuery);

        var page = await fixture.Service.GetCatalogPageAsync(null, ProductCatalogStatusFilter.All, 0, 50);

        Assert.Equal(1, auditQuery.GetRecentActivityCallCount);
        Assert.Single(page.Items);
        Assert.True(page.Items[0].HasRecentActivity);
        Assert.Equal("Cajero 02", page.Items[0].RecentActivity!.ActorDisplayName);
    }

    [Fact]
    public async Task GetCatalogPageAsyncDoesNotQueryRecentActivityWithoutViewProductAuditPermission()
    {
        var productId = ProductId.New();
        var catalogItem = new ProductCatalogItem(
            productId, "SKU-001", null, "Agua 1L", 10m, "MXN", false, 0m, 0m, true);
        var catalogQuery = new FakeProductCatalogQuery(new ProductCatalogPageResult([catalogItem], false));

        var auditQuery = new FakeProductAuditQuery();
        var fixture = CreateFixture(productAuditQuery: auditQuery, productCatalogQuery: catalogQuery);

        var page = await fixture.Service.GetCatalogPageAsync(null, ProductCatalogStatusFilter.All, 0, 50);

        Assert.Equal(0, auditQuery.GetRecentActivityCallCount);
        Assert.False(page.Items[0].HasRecentActivity);
    }
}
