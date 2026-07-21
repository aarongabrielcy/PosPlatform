using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Common.ValueObjects;
using Pos.Domain.Products;

namespace Pos.Domain.Tests.Products;

public class ProductTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static Product CreateProduct(
        ProductId? id = null,
        OrganizationId? organizationId = null,
        Sku? sku = null,
        Barcode? barcode = null,
        string name = "Producto de prueba",
        string? description = "Descripción de prueba",
        Money? salePrice = null,
        Money? cost = null,
        bool tracksInventory = true,
        DateTimeOffset? createdAtUtc = null) =>
        new(
            id ?? ProductId.New(),
            organizationId ?? OrganizationId.New(),
            sku ?? new Sku("PROD-001"),
            barcode,
            name,
            description,
            salePrice ?? new Money(100m, "MXN"),
            cost,
            tracksInventory,
            createdAtUtc ?? FixedUtcNow);

    [Fact]
    public void IsCreatedActive()
    {
        var product = CreateProduct();

        Assert.True(product.IsActive);
    }

    [Fact]
    public void PreservesId()
    {
        var id = ProductId.New();

        var product = CreateProduct(id: id);

        Assert.Equal(id, product.Id);
    }

    [Fact]
    public void PreservesOrganizationId()
    {
        var organizationId = OrganizationId.New();

        var product = CreateProduct(organizationId: organizationId);

        Assert.Equal(organizationId, product.OrganizationId);
    }

    [Fact]
    public void PreservesSku()
    {
        var sku = new Sku("PROD-002");

        var product = CreateProduct(sku: sku);

        Assert.Equal(sku, product.Sku);
    }

    [Fact]
    public void PreservesBarcode()
    {
        var barcode = new Barcode("1234567890");

        var product = CreateProduct(barcode: barcode);

        Assert.Equal(barcode, product.Barcode);
    }

    [Fact]
    public void AllowsNullBarcode()
    {
        var product = CreateProduct(barcode: null);

        Assert.Null(product.Barcode);
    }

    [Fact]
    public void TrimsName()
    {
        var product = CreateProduct(name: "  Producto  ");

        Assert.Equal("Producto", product.Name);
    }

    [Fact]
    public void TrimsDescription()
    {
        var product = CreateProduct(description: "  Descripción  ");

        Assert.Equal("Descripción", product.Description);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ConvertsBlankDescriptionToNull(string description)
    {
        var product = CreateProduct(description: description);

        Assert.Null(product.Description);
    }

    [Fact]
    public void PreservesNullDescription()
    {
        var product = CreateProduct(description: null);

        Assert.Null(product.Description);
    }

    [Fact]
    public void PreservesSalePrice()
    {
        var salePrice = new Money(150m, "MXN");

        var product = CreateProduct(salePrice: salePrice);

        Assert.Equal(salePrice, product.SalePrice);
    }

    [Fact]
    public void PreservesCost()
    {
        var cost = new Money(80m, "MXN");

        var product = CreateProduct(salePrice: new Money(150m, "MXN"), cost: cost);

        Assert.Equal(cost, product.Cost);
    }

    [Fact]
    public void PreservesNullCost()
    {
        var product = CreateProduct(cost: null);

        Assert.Null(product.Cost);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PreservesTracksInventory(bool tracksInventory)
    {
        var product = CreateProduct(tracksInventory: tracksInventory);

        Assert.Equal(tracksInventory, product.TracksInventory);
    }

    [Fact]
    public void PreservesCreatedAtUtc()
    {
        var product = CreateProduct(createdAtUtc: FixedUtcNow);

        Assert.Equal(FixedUtcNow, product.CreatedAtUtc);
    }

    [Fact]
    public void RejectsDefaultProductId()
    {
        Assert.Throws<DomainValidationException>(() => new Product(
            default,
            OrganizationId.New(),
            new Sku("PROD-001"),
            null,
            "Producto de prueba",
            null,
            new Money(100m, "MXN"),
            null,
            true,
            FixedUtcNow));
    }

    [Fact]
    public void RejectsDefaultOrganizationId()
    {
        Assert.Throws<DomainValidationException>(() => new Product(
            ProductId.New(),
            default,
            new Sku("PROD-001"),
            null,
            "Producto de prueba",
            null,
            new Money(100m, "MXN"),
            null,
            true,
            FixedUtcNow));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("A")]
    public void RejectsInvalidName(string? name)
    {
        Assert.Throws<DomainValidationException>(() => CreateProduct(name: name!));
    }

    [Fact]
    public void RejectsNameLongerThan160()
    {
        var tooLong = new string('A', 161);

        Assert.Throws<DomainValidationException>(() => CreateProduct(name: tooLong));
    }

    [Fact]
    public void RejectsDescriptionLongerThan500()
    {
        var tooLong = new string('A', 501);

        Assert.Throws<DomainValidationException>(() => CreateProduct(description: tooLong));
    }

    [Fact]
    public void RejectsNullSalePrice()
    {
        Assert.Throws<DomainValidationException>(() => new Product(
            ProductId.New(),
            OrganizationId.New(),
            new Sku("PROD-001"),
            null,
            "Producto de prueba",
            null,
            null!,
            null,
            true,
            FixedUtcNow));
    }

    [Fact]
    public void RejectsNegativeSalePrice()
    {
        Assert.Throws<DomainValidationException>(() => CreateProduct(salePrice: new Money(-1m, "MXN")));
    }

    [Fact]
    public void RejectsNegativeCost()
    {
        Assert.Throws<DomainValidationException>(
            () => CreateProduct(salePrice: new Money(100m, "MXN"), cost: new Money(-1m, "MXN")));
    }

    [Fact]
    public void RejectsMismatchedCurrenciesBetweenSalePriceAndCost()
    {
        Assert.Throws<DomainValidationException>(
            () => CreateProduct(salePrice: new Money(100m, "MXN"), cost: new Money(50m, "USD")));
    }

    [Fact]
    public void RejectsNonUtcCreatedAt()
    {
        var nonUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-5));

        Assert.Throws<DomainValidationException>(() => CreateProduct(createdAtUtc: nonUtc));
    }

    [Fact]
    public void RenameUpdatesValidName()
    {
        var product = CreateProduct();

        product.Rename("Nuevo Nombre");

        Assert.Equal("Nuevo Nombre", product.Name);
    }

    [Fact]
    public void RenameWithInvalidNamePreservesPreviousName()
    {
        var product = CreateProduct(name: "Nombre Original");

        Assert.Throws<DomainValidationException>(() => product.Rename("A"));

        Assert.Equal("Nombre Original", product.Name);
    }

    [Fact]
    public void ChangeDescriptionUpdatesValue()
    {
        var product = CreateProduct();

        product.ChangeDescription("Nueva descripción");

        Assert.Equal("Nueva descripción", product.Description);
    }

    [Fact]
    public void ChangeDescriptionAllowsRemovingDescription()
    {
        var product = CreateProduct(description: "Descripción original");

        product.ChangeDescription(null);

        Assert.Null(product.Description);
    }

    [Fact]
    public void ChangeDescriptionWithTooLongValuePreservesPreviousDescription()
    {
        var product = CreateProduct(description: "Descripción original");
        var tooLong = new string('A', 501);

        Assert.Throws<DomainValidationException>(() => product.ChangeDescription(tooLong));

        Assert.Equal("Descripción original", product.Description);
    }

    [Fact]
    public void ChangeSkuUpdatesValue()
    {
        var product = CreateProduct();
        var newSku = new Sku("PROD-999");

        product.ChangeSku(newSku);

        Assert.Equal(newSku, product.Sku);
    }

    [Fact]
    public void ChangeBarcodeUpdatesValue()
    {
        var product = CreateProduct();
        var newBarcode = new Barcode("9876543210");

        product.ChangeBarcode(newBarcode);

        Assert.Equal(newBarcode, product.Barcode);
    }

    [Fact]
    public void ChangeBarcodeAllowsRemovingBarcode()
    {
        var product = CreateProduct(barcode: new Barcode("1234567890"));

        product.ChangeBarcode(null);

        Assert.Null(product.Barcode);
    }

    [Fact]
    public void ChangeSalePriceUpdatesValidPrice()
    {
        var product = CreateProduct();

        product.ChangeSalePrice(new Money(200m, "MXN"));

        Assert.Equal(new Money(200m, "MXN"), product.SalePrice);
    }

    [Fact]
    public void ChangeSalePriceAllowsZero()
    {
        var product = CreateProduct();

        product.ChangeSalePrice(new Money(0m, "MXN"));

        Assert.Equal(new Money(0m, "MXN"), product.SalePrice);
    }

    [Fact]
    public void ChangeSalePriceRejectsNegativeAmount()
    {
        var product = CreateProduct();

        Assert.Throws<DomainValidationException>(() => product.ChangeSalePrice(new Money(-1m, "MXN")));
    }

    [Fact]
    public void ChangeSalePriceRejectsCurrencyIncompatibleWithCost()
    {
        var product = CreateProduct(salePrice: new Money(100m, "MXN"), cost: new Money(50m, "MXN"));

        Assert.Throws<DomainValidationException>(() => product.ChangeSalePrice(new Money(200m, "USD")));
    }

    [Fact]
    public void ChangeCostUpdatesValidCost()
    {
        var product = CreateProduct(salePrice: new Money(100m, "MXN"));

        product.ChangeCost(new Money(40m, "MXN"));

        Assert.Equal(new Money(40m, "MXN"), product.Cost);
    }

    [Fact]
    public void ChangeCostAllowsZero()
    {
        var product = CreateProduct(salePrice: new Money(100m, "MXN"));

        product.ChangeCost(new Money(0m, "MXN"));

        Assert.Equal(new Money(0m, "MXN"), product.Cost);
    }

    [Fact]
    public void ChangeCostAllowsRemovingCost()
    {
        var product = CreateProduct(salePrice: new Money(100m, "MXN"), cost: new Money(40m, "MXN"));

        product.ChangeCost(null);

        Assert.Null(product.Cost);
    }

    [Fact]
    public void ChangeCostRejectsNegativeAmount()
    {
        var product = CreateProduct(salePrice: new Money(100m, "MXN"));

        Assert.Throws<DomainValidationException>(() => product.ChangeCost(new Money(-1m, "MXN")));
    }

    [Fact]
    public void ChangeCostRejectsCurrencyIncompatibleWithSalePrice()
    {
        var product = CreateProduct(salePrice: new Money(100m, "MXN"));

        Assert.Throws<DomainValidationException>(() => product.ChangeCost(new Money(40m, "USD")));
    }

    [Fact]
    public void EnableInventoryTrackingActivatesTracking()
    {
        var product = CreateProduct(tracksInventory: false);

        product.EnableInventoryTracking();

        Assert.True(product.TracksInventory);
    }

    [Fact]
    public void DisableInventoryTrackingDeactivatesTracking()
    {
        var product = CreateProduct(tracksInventory: true);

        product.DisableInventoryTracking();

        Assert.False(product.TracksInventory);
    }

    [Fact]
    public void ActivateAndDeactivateChangeState()
    {
        var product = CreateProduct();

        product.Deactivate();
        Assert.False(product.IsActive);

        product.Activate();
        Assert.True(product.IsActive);
    }

    [Fact]
    public void ChangeSalePriceWithIncompatibleCurrencyDoesNotModifySalePriceOrCost()
    {
        var product = CreateProduct(salePrice: new Money(100m, "MXN"), cost: new Money(50m, "MXN"));

        Assert.Throws<DomainValidationException>(() => product.ChangeSalePrice(new Money(200m, "USD")));

        Assert.Equal(new Money(100m, "MXN"), product.SalePrice);
        Assert.Equal(new Money(50m, "MXN"), product.Cost);
    }

    [Fact]
    public void ChangeCostWithIncompatibleCurrencyDoesNotModifySalePriceOrCost()
    {
        var product = CreateProduct(salePrice: new Money(100m, "MXN"), cost: new Money(50m, "MXN"));

        Assert.Throws<DomainValidationException>(() => product.ChangeCost(new Money(30m, "USD")));

        Assert.Equal(new Money(100m, "MXN"), product.SalePrice);
        Assert.Equal(new Money(50m, "MXN"), product.Cost);
    }

    [Fact]
    public void RehydrateRestoresActiveState()
    {
        var id = ProductId.New();
        var organizationId = OrganizationId.New();
        var sku = new Sku("PROD-001");
        var barcode = new Barcode("1234567890");
        var salePrice = new Money(100m, "MXN");
        var cost = new Money(50m, "MXN");

        var product = Product.Rehydrate(
            id,
            organizationId,
            sku,
            barcode,
            "Producto de prueba",
            "Descripción",
            salePrice,
            cost,
            true,
            true,
            FixedUtcNow);

        Assert.Equal(id, product.Id);
        Assert.Equal(organizationId, product.OrganizationId);
        Assert.Equal(sku, product.Sku);
        Assert.Equal(barcode, product.Barcode);
        Assert.Equal("Producto de prueba", product.Name);
        Assert.Equal("Descripción", product.Description);
        Assert.Equal(salePrice, product.SalePrice);
        Assert.Equal(cost, product.Cost);
        Assert.True(product.TracksInventory);
        Assert.True(product.IsActive);
        Assert.Equal(FixedUtcNow, product.CreatedAtUtc);
    }

    [Fact]
    public void RehydrateRestoresInactiveState()
    {
        var product = Product.Rehydrate(
            ProductId.New(),
            OrganizationId.New(),
            new Sku("PROD-001"),
            null,
            "Producto de prueba",
            null,
            new Money(100m, "MXN"),
            null,
            true,
            false,
            FixedUtcNow);

        Assert.False(product.IsActive);
    }

    [Fact]
    public void RehydrateRejectsDefaultProductId()
    {
        Assert.Throws<DomainValidationException>(() => Product.Rehydrate(
            default,
            OrganizationId.New(),
            new Sku("PROD-001"),
            null,
            "Producto de prueba",
            null,
            new Money(100m, "MXN"),
            null,
            true,
            true,
            FixedUtcNow));
    }

    [Fact]
    public void RehydrateRejectsNegativeSalePrice()
    {
        Assert.Throws<DomainValidationException>(() => Product.Rehydrate(
            ProductId.New(),
            OrganizationId.New(),
            new Sku("PROD-001"),
            null,
            "Producto de prueba",
            null,
            new Money(-1m, "MXN"),
            null,
            true,
            true,
            FixedUtcNow));
    }

    [Fact]
    public void RehydrateRejectsMismatchedCurrenciesBetweenSalePriceAndCost()
    {
        Assert.Throws<DomainValidationException>(() => Product.Rehydrate(
            ProductId.New(),
            OrganizationId.New(),
            new Sku("PROD-001"),
            null,
            "Producto de prueba",
            null,
            new Money(100m, "MXN"),
            new Money(50m, "USD"),
            true,
            true,
            FixedUtcNow));
    }
}
