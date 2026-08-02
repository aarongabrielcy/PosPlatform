using Pos.Application.SalesCart;
using Pos.Desktop.Common;
using Pos.Domain.Common.Identifiers;

namespace Pos.Desktop.Tests.Common;

public class ProductAvailabilityStatusConverterTests
{
    [Fact]
    public void ShowsOutOfStockForATrackedProductWithoutQuantity()
    {
        var converter = new ProductAvailabilityStatusConverter();
        var result = new ProductSearchResult(
            ProductId.New(), "SKU-1", "Producto de prueba", 10m, "MXN", availableQuantity: 0m, tracksInventory: true);

        var text = converter.Convert(result, typeof(string), null, null!);

        Assert.Equal("Sin existencia", text);
    }

    [Fact]
    public void ShowsNothingForATrackedProductWithStock()
    {
        var converter = new ProductAvailabilityStatusConverter();
        var result = new ProductSearchResult(
            ProductId.New(), "SKU-1", "Producto de prueba", 10m, "MXN", availableQuantity: 5m, tracksInventory: true);

        var text = converter.Convert(result, typeof(string), null, null!);

        Assert.Equal(string.Empty, text);
    }

    [Fact]
    public void ShowsDoesNotTrackInventoryForAnUntrackedProduct()
    {
        var converter = new ProductAvailabilityStatusConverter();
        var result = new ProductSearchResult(
            ProductId.New(), "SKU-2", "Servicio", 10m, "MXN", availableQuantity: 0m, tracksInventory: false);

        var text = converter.Convert(result, typeof(string), null, null!);

        Assert.Equal("No controla inventario", text);
    }
}
