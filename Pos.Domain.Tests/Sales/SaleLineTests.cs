using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Common.ValueObjects;
using Pos.Domain.Products;
using Pos.Domain.Sales;

namespace Pos.Domain.Tests.Sales;

public class SaleLineTests
{
    private static SaleLine CreateLine(
        SaleLineId? id = null,
        ProductId? productId = null,
        Sku? productSku = null,
        string productName = "Producto de prueba",
        decimal quantity = 2m,
        Money? unitPrice = null) =>
        new(
            id ?? SaleLineId.New(),
            productId ?? ProductId.New(),
            productSku ?? new Sku("SKU-001"),
            productName,
            quantity,
            unitPrice ?? new Money(10m, "MXN"));

    // ---------- Creación ----------

    [Fact]
    public void PreservesId()
    {
        var id = SaleLineId.New();

        var line = CreateLine(id: id);

        Assert.Equal(id, line.Id);
    }

    [Fact]
    public void PreservesProductId()
    {
        var productId = ProductId.New();

        var line = CreateLine(productId: productId);

        Assert.Equal(productId, line.ProductId);
    }

    [Fact]
    public void PreservesProductSku()
    {
        var sku = new Sku("SKU-002");

        var line = CreateLine(productSku: sku);

        Assert.Equal(sku, line.ProductSku);
    }

    [Fact]
    public void TrimsProductName()
    {
        var line = CreateLine(productName: "  Producto  ");

        Assert.Equal("Producto", line.ProductName);
    }

    [Fact]
    public void PreservesQuantity()
    {
        var line = CreateLine(quantity: 3m);

        Assert.Equal(3m, line.Quantity);
    }

    [Fact]
    public void PreservesUnitPrice()
    {
        var unitPrice = new Money(25m, "MXN");

        var line = CreateLine(unitPrice: unitPrice);

        Assert.Equal(unitPrice, line.UnitPrice);
    }

    [Fact]
    public void CalculatesLineSubtotal()
    {
        var line = CreateLine(quantity: 3m, unitPrice: new Money(10m, "MXN"));

        Assert.Equal(new Money(30m, "MXN"), line.LineSubtotal);
    }

    [Fact]
    public void AllowsDecimalQuantity()
    {
        var line = CreateLine(quantity: 1.5m, unitPrice: new Money(10m, "MXN"));

        Assert.Equal(1.5m, line.Quantity);
        Assert.Equal(new Money(15m, "MXN"), line.LineSubtotal);
    }

    [Fact]
    public void AllowsZeroUnitPrice()
    {
        var line = CreateLine(unitPrice: new Money(0m, "MXN"));

        Assert.Equal(new Money(0m, "MXN"), line.UnitPrice);
        Assert.Equal(new Money(0m, "MXN"), line.LineSubtotal);
    }

    [Fact]
    public void RejectsDefaultSaleLineId()
    {
        Assert.Throws<DomainValidationException>(() => new SaleLine(
            default,
            ProductId.New(),
            new Sku("SKU-001"),
            "Producto de prueba",
            1m,
            new Money(10m, "MXN")));
    }

    [Fact]
    public void RejectsDefaultProductId()
    {
        Assert.Throws<DomainValidationException>(() => new SaleLine(
            SaleLineId.New(),
            default,
            new Sku("SKU-001"),
            "Producto de prueba",
            1m,
            new Money(10m, "MXN")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("A")]
    public void RejectsInvalidProductName(string? productName)
    {
        Assert.Throws<DomainValidationException>(() => CreateLine(productName: productName!));
    }

    [Fact]
    public void RejectsZeroQuantity()
    {
        Assert.Throws<DomainValidationException>(() => CreateLine(quantity: 0m));
    }

    [Fact]
    public void RejectsNegativeQuantity()
    {
        Assert.Throws<DomainValidationException>(() => CreateLine(quantity: -1m));
    }

    [Fact]
    public void RejectsNullUnitPrice()
    {
        Assert.Throws<DomainValidationException>(() => new SaleLine(
            SaleLineId.New(),
            ProductId.New(),
            new Sku("SKU-001"),
            "Producto de prueba",
            1m,
            null!));
    }

    [Fact]
    public void RejectsNegativeUnitPrice()
    {
        Assert.Throws<DomainValidationException>(() => CreateLine(unitPrice: new Money(-1m, "MXN")));
    }

    // ---------- ChangeQuantity ----------

    [Fact]
    public void ChangeQuantityUpdatesQuantity()
    {
        var line = CreateLine(quantity: 1m, unitPrice: new Money(10m, "MXN"));

        line.ChangeQuantity(5m);

        Assert.Equal(5m, line.Quantity);
    }

    [Fact]
    public void ChangeQuantityRecalculatesLineSubtotal()
    {
        var line = CreateLine(quantity: 1m, unitPrice: new Money(10m, "MXN"));

        line.ChangeQuantity(4m);

        Assert.Equal(new Money(40m, "MXN"), line.LineSubtotal);
    }

    [Fact]
    public void ChangeQuantityAllowsDecimalValues()
    {
        var line = CreateLine(quantity: 1m, unitPrice: new Money(10m, "MXN"));

        line.ChangeQuantity(2.5m);

        Assert.Equal(2.5m, line.Quantity);
        Assert.Equal(new Money(25m, "MXN"), line.LineSubtotal);
    }

    [Fact]
    public void ChangeQuantityRejectsZero()
    {
        var line = CreateLine();

        Assert.Throws<DomainValidationException>(() => line.ChangeQuantity(0m));
    }

    [Fact]
    public void ChangeQuantityRejectsNegative()
    {
        var line = CreateLine();

        Assert.Throws<DomainValidationException>(() => line.ChangeQuantity(-1m));
    }

    [Fact]
    public void InvalidChangeQuantityPreservesPreviousQuantityAndLineSubtotal()
    {
        var line = CreateLine(quantity: 3m, unitPrice: new Money(10m, "MXN"));

        Assert.Throws<DomainValidationException>(() => line.ChangeQuantity(-1m));

        Assert.Equal(3m, line.Quantity);
        Assert.Equal(new Money(30m, "MXN"), line.LineSubtotal);
    }
}
