using Pos.Application.Authentication;
using Pos.Application.Products.CreateProduct;
using Pos.Application.RegisterSessions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;

namespace Pos.Application.Tests.Products.CreateProduct;

public class CreateProductServiceTests
{
    private static readonly DateTimeOffset UtcNow = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    private sealed record Fixture(
        CreateProductService Service,
        FakeCurrentUserSession UserSession,
        FakeCurrentRegisterSession RegisterSession,
        FakeProductRepository ProductRepository,
        FakeInventoryItemRepository InventoryItemRepository,
        FakeUnitOfWork UnitOfWork,
        OrganizationId OrganizationId,
        BranchId BranchId);

    private static Fixture CreateFixture(bool authenticated = true, bool registerOpen = true, bool granted = true)
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
                granted ? new[] { Permission.ManageProducts } : Array.Empty<Permission>());
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
                UtcNow,
                100m,
                "MXN");
        }

        var productRepository = new FakeProductRepository();
        var inventoryItemRepository = new FakeInventoryItemRepository();
        var unitOfWork = new FakeUnitOfWork();
        var clock = new FakeClock(UtcNow);

        var service = new CreateProductService(
            userSession, registerSession, productRepository, inventoryItemRepository, unitOfWork, clock);

        return new Fixture(
            service, userSession, registerSession, productRepository, inventoryItemRepository, unitOfWork,
            organizationId, branchId);
    }

    private static CreateProductRequest ValidRequestWithoutInventory() =>
        new("SKU-001", null, "Producto de prueba", null, 10m, null, false, 0m, 0m);

    private static CreateProductRequest ValidRequestWithInventory() =>
        new("SKU-001", "7501234567890", "Producto de prueba", "Descripción", 10m, 5m, true, 8m, 2m);

    [Fact]
    public async Task CreateAsyncSucceedsWithoutInventoryWhenTracksInventoryIsFalse()
    {
        var fixture = CreateFixture();

        var result = await fixture.Service.CreateAsync(ValidRequestWithoutInventory(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("SKU-001", result.Sku);
        Assert.Equal(1, fixture.ProductRepository.AddCallCount);
        Assert.Equal(0, fixture.InventoryItemRepository.AddCallCount);
        Assert.Equal(1, fixture.UnitOfWork.CommitCallCount);
    }

    [Fact]
    public async Task CreateAsyncSucceedsWithInventoryWhenTracksInventoryIsTrue()
    {
        var fixture = CreateFixture();

        var result = await fixture.Service.CreateAsync(ValidRequestWithInventory(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, fixture.ProductRepository.AddCallCount);
        Assert.Equal(1, fixture.InventoryItemRepository.AddCallCount);
        Assert.Equal(1, fixture.UnitOfWork.CommitCallCount);

        var inventoryItem = await fixture.InventoryItemRepository.GetByBranchAndProductAsync(
            fixture.BranchId, result.ProductId!.Value, CancellationToken.None);
        Assert.NotNull(inventoryItem);
        Assert.Equal(8m, inventoryItem!.Quantity);
        Assert.Equal(2m, inventoryItem.ReorderPoint);
    }

    [Fact]
    public async Task CreateAsyncUsesRegisterSessionCurrencyForSalePriceAndCost()
    {
        var fixture = CreateFixture();

        var result = await fixture.Service.CreateAsync(ValidRequestWithInventory(), CancellationToken.None);

        Assert.True(result.Success);
        var product = await fixture.ProductRepository.GetByIdAsync(result.ProductId!.Value, CancellationToken.None);
        Assert.NotNull(product);
        Assert.Equal("MXN", product!.SalePrice.Currency);
        Assert.Equal("MXN", product.Cost!.Currency);
    }

    [Fact]
    public async Task CreateAsyncRejectsDuplicateSku()
    {
        var fixture = CreateFixture();
        await fixture.Service.CreateAsync(ValidRequestWithoutInventory(), CancellationToken.None);

        var duplicate = ValidRequestWithoutInventory() with { Name = "Otro nombre" };
        var result = await fixture.Service.CreateAsync(duplicate, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CreateProductResultStatus.DuplicateSku, result.Status);
        Assert.Equal(1, fixture.ProductRepository.AddCallCount);
    }

    [Fact]
    public async Task CreateAsyncRejectsDuplicateBarcode()
    {
        var fixture = CreateFixture();
        await fixture.Service.CreateAsync(ValidRequestWithInventory(), CancellationToken.None);

        var duplicate = new CreateProductRequest(
            "SKU-002", "7501234567890", "Otro producto", null, 10m, null, false, 0m, 0m);
        var result = await fixture.Service.CreateAsync(duplicate, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CreateProductResultStatus.DuplicateBarcode, result.Status);
    }

    [Fact]
    public async Task CreateAsyncRejectsNegativeSalePrice()
    {
        var fixture = CreateFixture();
        var request = ValidRequestWithoutInventory() with { SalePrice = -1m };

        var result = await fixture.Service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CreateProductResultStatus.InvalidSalePrice, result.Status);
        Assert.Equal(0, fixture.ProductRepository.AddCallCount);
    }

    [Fact]
    public async Task CreateAsyncRejectsNegativeCost()
    {
        var fixture = CreateFixture();
        var request = ValidRequestWithoutInventory() with { Cost = -1m };

        var result = await fixture.Service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CreateProductResultStatus.InvalidCost, result.Status);
    }

    [Fact]
    public async Task CreateAsyncRejectsNegativeInitialQuantity()
    {
        var fixture = CreateFixture();
        var request = ValidRequestWithInventory() with { InitialQuantity = -1m };

        var result = await fixture.Service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CreateProductResultStatus.InvalidInitialQuantity, result.Status);
        Assert.Equal(0, fixture.ProductRepository.AddCallCount);
    }

    [Fact]
    public async Task CreateAsyncRejectsNegativeReorderPoint()
    {
        var fixture = CreateFixture();
        var request = ValidRequestWithInventory() with { ReorderPoint = -1m };

        var result = await fixture.Service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CreateProductResultStatus.InvalidReorderPoint, result.Status);
    }

    [Fact]
    public async Task CreateAsyncRejectsInvalidSkuFormat()
    {
        var fixture = CreateFixture();
        var request = ValidRequestWithoutInventory() with { Sku = "  " };

        var result = await fixture.Service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CreateProductResultStatus.InvalidSku, result.Status);
    }

    [Fact]
    public async Task CreateAsyncRejectsInvalidBarcodeFormat()
    {
        var fixture = CreateFixture();
        var request = ValidRequestWithoutInventory() with { Barcode = "ABC" };

        var result = await fixture.Service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CreateProductResultStatus.InvalidBarcode, result.Status);
    }

    [Fact]
    public async Task CreateAsyncRejectsBlankName()
    {
        var fixture = CreateFixture();
        var request = ValidRequestWithoutInventory() with { Name = "   " };

        var result = await fixture.Service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CreateProductResultStatus.InvalidName, result.Status);
    }

    [Fact]
    public async Task CreateAsyncFailsWhenUserIsNotAuthenticated()
    {
        var fixture = CreateFixture(authenticated: false);

        var result = await fixture.Service.CreateAsync(ValidRequestWithoutInventory(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CreateProductResultStatus.NotAuthenticated, result.Status);
    }

    [Fact]
    public async Task CreateAsyncFailsWhenUserLacksManageProductsPermission()
    {
        var fixture = CreateFixture(granted: false);

        var result = await fixture.Service.CreateAsync(ValidRequestWithoutInventory(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CreateProductResultStatus.NotAuthorized, result.Status);
    }

    [Fact]
    public async Task CreateAsyncFailsWhenRegisterSessionIsNotOpen()
    {
        var fixture = CreateFixture(registerOpen: false);

        var result = await fixture.Service.CreateAsync(ValidRequestWithoutInventory(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CreateProductResultStatus.RegisterSessionRequired, result.Status);
    }

    [Fact]
    public async Task CreateAsyncRejectsNullRequest()
    {
        var fixture = CreateFixture();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => fixture.Service.CreateAsync(null!, CancellationToken.None));
    }
}
