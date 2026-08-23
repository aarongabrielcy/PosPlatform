using Pos.Application.Authentication;
using Pos.Application.Enforcement;
using Pos.Application.Products.ManageProduct;
using Pos.Application.RegisterSessions;
using Pos.Application.Tests.Common.Time;
using Pos.Application.Tests.Enforcement;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Common.ValueObjects;
using Pos.Domain.Products;
using Pos.Domain.Security;

namespace Pos.Application.Tests.Products.ManageProduct;

// Pruebas de ProductPhotoService (BASIC-UX-01, sección 47/48): Product photo es opcional, agregar/
// reemplazar/quitar nunca cambia ProductId, Cashier no puede mutar la foto, Manager/Admin con
// ManageProducts sí puede.
public class ProductPhotoServiceTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset UtcNow = new(2026, 1, 2, 9, 0, 0, TimeSpan.Zero);

    private sealed record Fixture(
        ProductPhotoService Service,
        FakeCurrentUserSession UserSession,
        FakeProductRepository ProductRepository,
        FakeProductAuditRepository ProductAuditRepository,
        FakeAdministrativeNotificationWriter AdministrativeNotificationWriter,
        FakeInstallationEnforcementStateService EnforcementStateService,
        FakeUnitOfWork UnitOfWork,
        FakeProductImageStore ProductImageStore,
        OrganizationId OrganizationId);

    private static Fixture CreateFixture(
        bool authenticated = true,
        IEnumerable<Permission>? permissions = null,
        InstallationEnforcementState enforcementState = InstallationEnforcementState.Allowed)
    {
        var organizationId = OrganizationId.New();

        var userSession = new FakeCurrentUserSession();

        if (authenticated)
        {
            userSession.CurrentUser = new AuthenticatedUser(
                UserId.New(), organizationId, RoleId.New(), "JPEREZ", "Juan Pérez", "Gerente",
                permissions ?? [Permission.ManageProducts, Permission.ViewProducts]);
        }

        var productRepository = new FakeProductRepository();
        var inventoryItemRepository = new FakeInventoryItemRepository();
        var productAuditRepository = new FakeProductAuditRepository();
        var administrativeNotificationWriter = new FakeAdministrativeNotificationWriter();
        var enforcementStateService = new FakeInstallationEnforcementStateService(enforcementState);
        var unitOfWork = new FakeUnitOfWork();
        var clock = new FakeClock(UtcNow);
        var productImageStore = new FakeProductImageStore();
        var registerSession = new FakeCurrentRegisterSession();

        var service = new ProductPhotoService(
            userSession, registerSession, productRepository, inventoryItemRepository, productAuditRepository,
            administrativeNotificationWriter, enforcementStateService, unitOfWork, clock, productImageStore);

        return new Fixture(
            service, userSession, productRepository, productAuditRepository, administrativeNotificationWriter,
            enforcementStateService, unitOfWork, productImageStore, organizationId);
    }

    private static Product CreateProduct(OrganizationId organizationId) =>
        new(
            ProductId.New(), organizationId, new Sku("SKU-001"), null, "Producto de prueba", null,
            new Money(10m, "MXN"), null, false, CreatedAtUtc);

    [Fact]
    public void ProductWithoutPhotoIsValidAndImageFileNameIsNull()
    {
        var product = CreateProduct(OrganizationId.New());

        Assert.Null(product.ImageFileName);
    }

    [Fact]
    public async Task AddingAPhotoDoesNotChangeProductId()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId);
        fixture.ProductRepository.Add(product);
        var originalId = product.Id;

        var result = await fixture.Service.SetProductImageAsync(product.Id, [1, 2, 3]);

        Assert.True(result.Success);
        Assert.Equal(originalId, result.Product!.ProductId);
        Assert.NotNull(result.Product.ImageFileName);
    }

    [Fact]
    public async Task ReplacingAPhotoDoesNotChangeProductIdAndDeletesThePreviousManagedFile()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId);
        fixture.ProductRepository.Add(product);
        var originalId = product.Id;

        var firstResult = await fixture.Service.SetProductImageAsync(product.Id, [1, 2, 3]);
        var firstFileName = firstResult.Product!.ImageFileName;

        var secondResult = await fixture.Service.SetProductImageAsync(product.Id, [4, 5, 6]);

        Assert.True(secondResult.Success);
        Assert.Equal(originalId, secondResult.Product!.ProductId);
        Assert.NotEqual(firstFileName, secondResult.Product.ImageFileName);
        Assert.Contains(firstFileName!, fixture.ProductImageStore.DeletedFiles);
    }

    [Fact]
    public async Task RemovingAPhotoDoesNotChangeProductIdAndDeletesTheManagedFile()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId);
        fixture.ProductRepository.Add(product);
        var originalId = product.Id;

        var setResult = await fixture.Service.SetProductImageAsync(product.Id, [1, 2, 3]);
        var fileName = setResult.Product!.ImageFileName;

        var removeResult = await fixture.Service.RemoveProductImageAsync(product.Id);

        Assert.True(removeResult.Success);
        Assert.Equal(originalId, removeResult.Product!.ProductId);
        Assert.Null(removeResult.Product.ImageFileName);
        Assert.Contains(fileName!, fixture.ProductImageStore.DeletedFiles);
    }

    [Fact]
    public async Task RemovingAPhotoWhenThereIsNoneIsANoOpThatStillSucceeds()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId);
        fixture.ProductRepository.Add(product);

        var result = await fixture.Service.RemoveProductImageAsync(product.Id);

        Assert.True(result.Success);
        Assert.Equal(0, fixture.ProductAuditRepository.AddCallCount);
        Assert.Equal(0, fixture.ProductImageStore.DeleteCallCount);
    }

    [Fact]
    public async Task CashierWithoutManageProductsCannotSetOrRemoveThePhoto()
    {
        var fixture = CreateFixture(permissions: [Permission.ViewProducts]);
        var product = CreateProduct(fixture.OrganizationId);
        fixture.ProductRepository.Add(product);

        var setResult = await fixture.Service.SetProductImageAsync(product.Id, [1, 2, 3]);
        var removeResult = await fixture.Service.RemoveProductImageAsync(product.Id);

        Assert.Equal(UpdateProductResultStatus.NotAuthorized, setResult.Status);
        Assert.Equal(UpdateProductResultStatus.NotAuthorized, removeResult.Status);
        Assert.Equal(0, fixture.ProductImageStore.SaveCallCount);
    }

    [Fact]
    public async Task ManagerWithManageProductsCanSetThePhoto()
    {
        var fixture = CreateFixture(permissions: [Permission.ManageProducts]);
        var product = CreateProduct(fixture.OrganizationId);
        fixture.ProductRepository.Add(product);

        var result = await fixture.Service.SetProductImageAsync(product.Id, [1, 2, 3]);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task InvalidImageIsRejectedWithoutMutatingTheProduct()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId);
        fixture.ProductRepository.Add(product);
        fixture.ProductImageStore.NextSaveStatus = ProductImageStoreStatus.InvalidImage;

        var result = await fixture.Service.SetProductImageAsync(product.Id, [0xFF]);

        Assert.Equal(UpdateProductResultStatus.InvalidImage, result.Status);
        Assert.Null(product.ImageFileName);
        Assert.Equal(0, fixture.ProductRepository.UpdateCallCount);
    }

    [Fact]
    public async Task OversizedImageIsRejectedWithoutMutatingTheProduct()
    {
        var fixture = CreateFixture();
        var product = CreateProduct(fixture.OrganizationId);
        fixture.ProductRepository.Add(product);
        fixture.ProductImageStore.NextSaveStatus = ProductImageStoreStatus.TooLarge;

        var result = await fixture.Service.SetProductImageAsync(product.Id, [0xFF]);

        Assert.Equal(UpdateProductResultStatus.ImageTooLarge, result.Status);
        Assert.Null(product.ImageFileName);
    }

    [Fact]
    public async Task InstallationRestrictedBlocksSettingThePhoto()
    {
        var fixture = CreateFixture(enforcementState: InstallationEnforcementState.Suspended);
        var product = CreateProduct(fixture.OrganizationId);
        fixture.ProductRepository.Add(product);

        var result = await fixture.Service.SetProductImageAsync(product.Id, [1, 2, 3]);

        Assert.Equal(UpdateProductResultStatus.InstallationRestricted, result.Status);
        Assert.Equal(0, fixture.ProductImageStore.SaveCallCount);
    }
}
