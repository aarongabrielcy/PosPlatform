using Pos.Application.Products.ManageProduct;
using Pos.Desktop.Common;
using Pos.Domain.Common.Identifiers;

namespace Pos.Desktop.Tests.Common;

public class ProductCatalogConvertersTests
{
    private static ProductCatalogItem CreateItem(
        bool tracksInventory = true, decimal quantity = 5m, decimal reorderPoint = 2m, bool isActive = true) =>
        new(ProductId.New(), "SKU-001", "7501234567890", "Producto de prueba", 10m, "MXN",
            tracksInventory, quantity, reorderPoint, isActive);

    [Fact]
    public void InventoryStatusConverterShowsOutOfStockWhenQuantityIsZeroOrLess()
    {
        var converter = new ProductCatalogInventoryStatusConverter();
        var item = CreateItem(quantity: 0m);

        Assert.Equal("Sin existencia", converter.Convert(item, typeof(string), null, null!));
    }

    [Fact]
    public void InventoryStatusConverterShowsLowStockWhenQuantityIsAtOrBelowReorderPoint()
    {
        var converter = new ProductCatalogInventoryStatusConverter();
        var item = CreateItem(quantity: 2m, reorderPoint: 2m);

        Assert.Equal("Stock bajo", converter.Convert(item, typeof(string), null, null!));
    }

    [Fact]
    public void InventoryStatusConverterShowsNothingWhenStockIsHealthy()
    {
        var converter = new ProductCatalogInventoryStatusConverter();
        var item = CreateItem(quantity: 20m, reorderPoint: 2m);

        Assert.Equal(string.Empty, converter.Convert(item, typeof(string), null, null!));
    }

    [Fact]
    public void InventoryStatusConverterShowsDoesNotTrackInventoryForAnUntrackedProductEvenWithZeroQuantity()
    {
        var converter = new ProductCatalogInventoryStatusConverter();
        var item = CreateItem(tracksInventory: false, quantity: 0m);

        Assert.Equal("No controla inventario", converter.Convert(item, typeof(string), null, null!));
    }

    [Fact]
    public void QuantityTextConverterShowsDoesNotTrackInventoryForAnUntrackedProduct()
    {
        var converter = new ProductCatalogQuantityTextConverter();
        var item = CreateItem(tracksInventory: false);

        Assert.Equal("No controla inventario", converter.Convert(item, typeof(string), "Quantity", null!));
    }

    [Fact]
    public void QuantityTextConverterShowsTheRawQuantityWhenTracked()
    {
        var converter = new ProductCatalogQuantityTextConverter();
        var item = CreateItem(quantity: 7m);

        Assert.Equal("7", converter.Convert(item, typeof(string), "Quantity", null!));
    }

    [Fact]
    public void QuantityTextConverterShowsTheReorderPointWhenParameterized()
    {
        var converter = new ProductCatalogQuantityTextConverter();
        var item = CreateItem(reorderPoint: 3m);

        Assert.Equal("3", converter.Convert(item, typeof(string), "ReorderPoint", null!));
    }

    [Fact]
    public void BooleanToActiveStatusTextConverterMapsTrueAndFalse()
    {
        var converter = new BooleanToActiveStatusTextConverter();

        Assert.Equal("Activo", converter.Convert(true, typeof(string), null, null!));
        Assert.Equal("Inactivo", converter.Convert(false, typeof(string), null, null!));
    }
}
