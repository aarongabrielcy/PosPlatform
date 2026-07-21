using Pos.Infrastructure.Persistence.Exceptions;
using Pos.Infrastructure.Persistence.Mappers;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Tests.Persistence.Mappers;

public class ProductMapperTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    private static ProductRecord CreateValidRecord() => new()
    {
        Id = Guid.NewGuid(),
        OrganizationId = Guid.NewGuid(),
        Sku = "PROD-001",
        Barcode = "1234567890",
        Name = "Producto de prueba",
        Description = "Descripción de prueba",
        SalePriceAmount = 100m,
        SalePriceCurrency = "MXN",
        CostAmount = 50m,
        CostCurrency = "MXN",
        TracksInventory = true,
        IsActive = true,
        CreatedAtUtc = CreatedAtUtc,
    };

    // ---------- ToDomain ----------

    [Fact]
    public void ToDomainReconstructsIdentifiers()
    {
        var record = CreateValidRecord();

        var product = ProductMapper.ToDomain(record);

        Assert.Equal(record.Id, product.Id.Value);
        Assert.Equal(record.OrganizationId, product.OrganizationId.Value);
    }

    [Fact]
    public void ToDomainKeepsSku()
    {
        var record = CreateValidRecord();

        var product = ProductMapper.ToDomain(record);

        Assert.Equal(record.Sku, product.Sku.Value);
    }

    [Fact]
    public void ToDomainKeepsBarcodeWhenPresent()
    {
        var record = CreateValidRecord();

        var product = ProductMapper.ToDomain(record);

        Assert.Equal(record.Barcode, product.Barcode!.Value.Value);
    }

    [Fact]
    public void ToDomainKeepsNullBarcode()
    {
        var record = CreateValidRecord();
        record.Barcode = null;

        var product = ProductMapper.ToDomain(record);

        Assert.Null(product.Barcode);
    }

    [Fact]
    public void ToDomainKeepsNameAndDescription()
    {
        var record = CreateValidRecord();

        var product = ProductMapper.ToDomain(record);

        Assert.Equal(record.Name, product.Name);
        Assert.Equal(record.Description, product.Description);
    }

    [Fact]
    public void ToDomainKeepsNullDescription()
    {
        var record = CreateValidRecord();
        record.Description = null;

        var product = ProductMapper.ToDomain(record);

        Assert.Null(product.Description);
    }

    [Fact]
    public void ToDomainKeepsSalePrice()
    {
        var record = CreateValidRecord();

        var product = ProductMapper.ToDomain(record);

        Assert.Equal(record.SalePriceAmount, product.SalePrice.Amount);
        Assert.Equal(record.SalePriceCurrency, product.SalePrice.Currency);
    }

    [Fact]
    public void ToDomainKeepsCostWhenPresent()
    {
        var record = CreateValidRecord();

        var product = ProductMapper.ToDomain(record);

        Assert.NotNull(product.Cost);
        Assert.Equal(record.CostAmount, product.Cost!.Amount);
        Assert.Equal(record.CostCurrency, product.Cost.Currency);
    }

    [Fact]
    public void ToDomainKeepsNullCost()
    {
        var record = CreateValidRecord();
        record.CostAmount = null;
        record.CostCurrency = null;

        var product = ProductMapper.ToDomain(record);

        Assert.Null(product.Cost);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ToDomainKeepsTracksInventory(bool tracksInventory)
    {
        var record = CreateValidRecord();
        record.TracksInventory = tracksInventory;

        var product = ProductMapper.ToDomain(record);

        Assert.Equal(tracksInventory, product.TracksInventory);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ToDomainKeepsIsActive(bool isActive)
    {
        var record = CreateValidRecord();
        record.IsActive = isActive;

        var product = ProductMapper.ToDomain(record);

        Assert.Equal(isActive, product.IsActive);
    }

    [Fact]
    public void ToDomainKeepsCreatedAtUtc()
    {
        var record = CreateValidRecord();

        var product = ProductMapper.ToDomain(record);

        Assert.Equal(CreatedAtUtc, product.CreatedAtUtc);
        Assert.Equal(TimeSpan.Zero, product.CreatedAtUtc.Offset);
    }

    [Fact]
    public void ToDomainRejectsNullRecord()
    {
        Assert.Throws<ArgumentNullException>(() => ProductMapper.ToDomain(null!));
    }

    [Fact]
    public void ToDomainWrapsEmptyIdInPersistenceDataException()
    {
        var record = CreateValidRecord();
        record.Id = Guid.Empty;

        Assert.Throws<PersistenceDataException>(() => ProductMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsEmptyOrganizationIdInPersistenceDataException()
    {
        var record = CreateValidRecord();
        record.OrganizationId = Guid.Empty;

        Assert.Throws<PersistenceDataException>(() => ProductMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsInvalidSkuInPersistenceDataException()
    {
        var record = CreateValidRecord();
        record.Sku = "*";

        Assert.Throws<PersistenceDataException>(() => ProductMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsInvalidBarcodeInPersistenceDataException()
    {
        var record = CreateValidRecord();
        record.Barcode = "abc";

        Assert.Throws<PersistenceDataException>(() => ProductMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsNegativeSalePriceInPersistenceDataException()
    {
        var record = CreateValidRecord();
        record.SalePriceAmount = -1m;

        Assert.Throws<PersistenceDataException>(() => ProductMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsInvalidCurrencyInPersistenceDataException()
    {
        var record = CreateValidRecord();
        record.SalePriceCurrency = "M";

        Assert.Throws<PersistenceDataException>(() => ProductMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsMismatchedCurrenciesInPersistenceDataException()
    {
        var record = CreateValidRecord();
        record.CostCurrency = "USD";

        Assert.Throws<PersistenceDataException>(() => ProductMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsPartialCostDataInPersistenceDataException()
    {
        var record = CreateValidRecord();
        record.CostAmount = null;
        record.CostCurrency = "MXN";

        Assert.Throws<PersistenceDataException>(() => ProductMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsPartialCostDataMissingCurrencyInPersistenceDataException()
    {
        var record = CreateValidRecord();
        record.CostAmount = 50m;
        record.CostCurrency = null;

        Assert.Throws<PersistenceDataException>(() => ProductMapper.ToDomain(record));
    }

    // ---------- ToRecord ----------

    [Fact]
    public void ToRecordKeepsAllFields()
    {
        var product = ProductMapper.ToDomain(CreateValidRecord());

        var record = ProductMapper.ToRecord(product);

        Assert.Equal(product.Id.Value, record.Id);
        Assert.Equal(product.OrganizationId.Value, record.OrganizationId);
        Assert.Equal(product.Sku.Value, record.Sku);
        Assert.Equal(product.Barcode!.Value.Value, record.Barcode);
        Assert.Equal(product.Name, record.Name);
        Assert.Equal(product.Description, record.Description);
        Assert.Equal(product.SalePrice.Amount, record.SalePriceAmount);
        Assert.Equal(product.SalePrice.Currency, record.SalePriceCurrency);
        Assert.Equal(product.Cost!.Amount, record.CostAmount);
        Assert.Equal(product.Cost.Currency, record.CostCurrency);
        Assert.Equal(product.TracksInventory, record.TracksInventory);
        Assert.Equal(product.IsActive, record.IsActive);
        Assert.Equal(product.CreatedAtUtc, record.CreatedAtUtc);
    }

    [Fact]
    public void ToRecordKeepsNullBarcodeAndCost()
    {
        var record = CreateValidRecord();
        record.Barcode = null;
        record.CostAmount = null;
        record.CostCurrency = null;
        var product = ProductMapper.ToDomain(record);

        var result = ProductMapper.ToRecord(product);

        Assert.Null(result.Barcode);
        Assert.Null(result.CostAmount);
        Assert.Null(result.CostCurrency);
    }

    [Fact]
    public void ToRecordRejectsNullProduct()
    {
        Assert.Throws<ArgumentNullException>(() => ProductMapper.ToRecord(null!));
    }

    // ---------- UpdateRecord ----------

    [Fact]
    public void UpdateRecordUpdatesSku()
    {
        var record = CreateValidRecord();
        var product = ProductMapper.ToDomain(record);
        product.ChangeSku(new Pos.Domain.Products.Sku("PROD-999"));

        ProductMapper.UpdateRecord(product, record);

        Assert.Equal("PROD-999", record.Sku);
    }

    [Fact]
    public void UpdateRecordUpdatesBarcodeToValue()
    {
        var record = CreateValidRecord();
        record.Barcode = null;
        var product = ProductMapper.ToDomain(record);
        product.ChangeBarcode(new Pos.Domain.Products.Barcode("9876543210"));

        ProductMapper.UpdateRecord(product, record);

        Assert.Equal("9876543210", record.Barcode);
    }

    [Fact]
    public void UpdateRecordUpdatesBarcodeToNull()
    {
        var record = CreateValidRecord();
        var product = ProductMapper.ToDomain(record);
        product.ChangeBarcode(null);

        ProductMapper.UpdateRecord(product, record);

        Assert.Null(record.Barcode);
    }

    [Fact]
    public void UpdateRecordUpdatesNameAndDescription()
    {
        var record = CreateValidRecord();
        var product = ProductMapper.ToDomain(record);
        product.Rename("Nuevo nombre");
        product.ChangeDescription("Nueva descripción");

        ProductMapper.UpdateRecord(product, record);

        Assert.Equal("Nuevo nombre", record.Name);
        Assert.Equal("Nueva descripción", record.Description);
    }

    [Fact]
    public void UpdateRecordUpdatesSalePrice()
    {
        var record = CreateValidRecord();
        var product = ProductMapper.ToDomain(record);
        product.ChangeSalePrice(new Pos.Domain.Common.ValueObjects.Money(200m, "MXN"));

        ProductMapper.UpdateRecord(product, record);

        Assert.Equal(200m, record.SalePriceAmount);
        Assert.Equal("MXN", record.SalePriceCurrency);
    }

    [Fact]
    public void UpdateRecordUpdatesCostToNull()
    {
        var record = CreateValidRecord();
        var product = ProductMapper.ToDomain(record);
        product.ChangeCost(null);

        ProductMapper.UpdateRecord(product, record);

        Assert.Null(record.CostAmount);
        Assert.Null(record.CostCurrency);
    }

    [Fact]
    public void UpdateRecordUpdatesTracksInventory()
    {
        var record = CreateValidRecord();
        var product = ProductMapper.ToDomain(record);
        product.DisableInventoryTracking();

        ProductMapper.UpdateRecord(product, record);

        Assert.False(record.TracksInventory);
    }

    [Fact]
    public void UpdateRecordUpdatesIsActive()
    {
        var record = CreateValidRecord();
        var product = ProductMapper.ToDomain(record);
        product.Deactivate();

        ProductMapper.UpdateRecord(product, record);

        Assert.False(record.IsActive);
    }

    [Fact]
    public void UpdateRecordDoesNotChangeId()
    {
        var record = CreateValidRecord();
        var originalId = record.Id;
        var product = ProductMapper.ToDomain(record);

        ProductMapper.UpdateRecord(product, record);

        Assert.Equal(originalId, record.Id);
    }

    [Fact]
    public void UpdateRecordDoesNotChangeOrganizationId()
    {
        var record = CreateValidRecord();
        var originalOrganizationId = record.OrganizationId;
        var product = ProductMapper.ToDomain(record);

        ProductMapper.UpdateRecord(product, record);

        Assert.Equal(originalOrganizationId, record.OrganizationId);
    }

    [Fact]
    public void UpdateRecordDoesNotChangeCreatedAtUtc()
    {
        var record = CreateValidRecord();
        var product = ProductMapper.ToDomain(record);

        ProductMapper.UpdateRecord(product, record);

        Assert.Equal(CreatedAtUtc, record.CreatedAtUtc);
    }

    [Fact]
    public void UpdateRecordRejectsMismatchedId()
    {
        var record = CreateValidRecord();
        var product = ProductMapper.ToDomain(record);
        record.Id = Guid.NewGuid();

        Assert.Throws<PersistenceDataException>(() => ProductMapper.UpdateRecord(product, record));
    }

    [Fact]
    public void UpdateRecordRejectsMismatchedOrganizationId()
    {
        var record = CreateValidRecord();
        var product = ProductMapper.ToDomain(record);
        record.OrganizationId = Guid.NewGuid();

        Assert.Throws<PersistenceDataException>(() => ProductMapper.UpdateRecord(product, record));
    }

    [Fact]
    public void UpdateRecordRejectsMismatchedCreatedAtUtc()
    {
        var record = CreateValidRecord();
        var product = ProductMapper.ToDomain(record);
        record.CreatedAtUtc = CreatedAtUtc.AddDays(1);

        Assert.Throws<PersistenceDataException>(() => ProductMapper.UpdateRecord(product, record));
    }

    [Fact]
    public void UpdateRecordRejectsNullProduct()
    {
        var record = CreateValidRecord();

        Assert.Throws<ArgumentNullException>(() => ProductMapper.UpdateRecord(null!, record));
    }

    [Fact]
    public void UpdateRecordRejectsNullRecord()
    {
        var product = ProductMapper.ToDomain(CreateValidRecord());

        Assert.Throws<ArgumentNullException>(() => ProductMapper.UpdateRecord(product, null!));
    }
}
