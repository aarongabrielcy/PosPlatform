using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Common.ValueObjects;
using Pos.Domain.Products;
using Pos.Domain.Sales;

namespace Pos.Domain.Tests.Sales;

public class SaleTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    private static Sale CreateSale(
        SaleId? id = null,
        OrganizationId? organizationId = null,
        BranchId? branchId = null,
        RegisterSessionId? registerSessionId = null,
        UserId? createdByUserId = null,
        string currency = "MXN",
        DateTimeOffset? createdAtUtc = null) =>
        new(
            id ?? SaleId.New(),
            organizationId ?? OrganizationId.New(),
            branchId ?? BranchId.New(),
            registerSessionId ?? RegisterSessionId.New(),
            createdByUserId ?? UserId.New(),
            currency,
            createdAtUtc ?? CreatedAtUtc);

    private static void AddLine(
        Sale sale,
        SaleLineId? saleLineId = null,
        ProductId? productId = null,
        Sku? productSku = null,
        string productName = "Producto de prueba",
        decimal quantity = 1m,
        Money? unitPrice = null) =>
        sale.AddLine(
            saleLineId ?? SaleLineId.New(),
            productId ?? ProductId.New(),
            productSku ?? new Sku("SKU-001"),
            productName,
            quantity,
            unitPrice ?? new Money(10m, "MXN"));

    // ---------- Creación ----------

    [Fact]
    public void PreservesAllIdentifiers()
    {
        var id = SaleId.New();
        var organizationId = OrganizationId.New();
        var branchId = BranchId.New();
        var registerSessionId = RegisterSessionId.New();
        var createdByUserId = UserId.New();

        var sale = new Sale(id, organizationId, branchId, registerSessionId, createdByUserId, "MXN", CreatedAtUtc);

        Assert.Equal(id, sale.Id);
        Assert.Equal(organizationId, sale.OrganizationId);
        Assert.Equal(branchId, sale.BranchId);
        Assert.Equal(registerSessionId, sale.RegisterSessionId);
        Assert.Equal(createdByUserId, sale.CreatedByUserId);
    }

    [Fact]
    public void PreservesCreatedAtUtc()
    {
        var sale = CreateSale(createdAtUtc: CreatedAtUtc);

        Assert.Equal(CreatedAtUtc, sale.CreatedAtUtc);
    }

    [Fact]
    public void NewSaleStartsWithoutLines()
    {
        var sale = CreateSale();

        Assert.Empty(sale.Lines);
    }

    [Fact]
    public void NewSaleStartsWithZeroSubtotalAndTotal()
    {
        var sale = CreateSale(currency: "MXN");

        Assert.Equal(new Money(0m, "MXN"), sale.Subtotal);
        Assert.Equal(new Money(0m, "MXN"), sale.Total);
    }

    [Fact]
    public void NormalizesCurrencyThroughMoney()
    {
        var sale = CreateSale(currency: "mxn");

        Assert.Equal("MXN", sale.Subtotal.Currency);
        Assert.Equal("MXN", sale.Total.Currency);
    }

    [Fact]
    public void RejectsDefaultSaleId()
    {
        Assert.Throws<DomainValidationException>(() => new Sale(
            default, OrganizationId.New(), BranchId.New(), RegisterSessionId.New(), UserId.New(), "MXN", CreatedAtUtc));
    }

    [Fact]
    public void RejectsDefaultOrganizationId()
    {
        Assert.Throws<DomainValidationException>(() => new Sale(
            SaleId.New(), default, BranchId.New(), RegisterSessionId.New(), UserId.New(), "MXN", CreatedAtUtc));
    }

    [Fact]
    public void RejectsDefaultBranchId()
    {
        Assert.Throws<DomainValidationException>(() => new Sale(
            SaleId.New(), OrganizationId.New(), default, RegisterSessionId.New(), UserId.New(), "MXN", CreatedAtUtc));
    }

    [Fact]
    public void RejectsDefaultRegisterSessionId()
    {
        Assert.Throws<DomainValidationException>(() => new Sale(
            SaleId.New(), OrganizationId.New(), BranchId.New(), default, UserId.New(), "MXN", CreatedAtUtc));
    }

    [Fact]
    public void RejectsDefaultCreatedByUserId()
    {
        Assert.Throws<DomainValidationException>(() => new Sale(
            SaleId.New(), OrganizationId.New(), BranchId.New(), RegisterSessionId.New(), default, "MXN", CreatedAtUtc));
    }

    [Theory]
    [InlineData("")]
    [InlineData("M")]
    [InlineData("MXNN")]
    public void RejectsInvalidCurrency(string currency)
    {
        Assert.Throws<DomainValidationException>(() => CreateSale(currency: currency));
    }

    [Fact]
    public void RejectsNonUtcCreatedAt()
    {
        var nonUtc = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.FromHours(-5));

        Assert.Throws<DomainValidationException>(() => CreateSale(createdAtUtc: nonUtc));
    }

    // ---------- AddLine ----------

    [Fact]
    public void AddLineAddsAValidLine()
    {
        var sale = CreateSale();
        var saleLineId = SaleLineId.New();

        AddLine(sale, saleLineId: saleLineId);

        Assert.Single(sale.Lines);
        Assert.Contains(sale.Lines, line => line.Id == saleLineId);
    }

    [Fact]
    public void AddLinePreservesSnapshotOfSkuNameAndPrice()
    {
        var sale = CreateSale();
        var sku = new Sku("SKU-777");
        var unitPrice = new Money(15m, "MXN");

        AddLine(sale, productSku: sku, productName: "  Nombre Producto  ", unitPrice: unitPrice);

        var line = sale.Lines.Single();
        Assert.Equal(sku, line.ProductSku);
        Assert.Equal("Nombre Producto", line.ProductName);
        Assert.Equal(unitPrice, line.UnitPrice);
    }

    [Fact]
    public void AddLineRecalculatesSubtotal()
    {
        var sale = CreateSale(currency: "MXN");

        AddLine(sale, quantity: 2m, unitPrice: new Money(10m, "MXN"));
        AddLine(sale, productId: ProductId.New(), quantity: 3m, unitPrice: new Money(5m, "MXN"));

        Assert.Equal(new Money(35m, "MXN"), sale.Subtotal);
    }

    [Fact]
    public void TotalEqualsSubtotal()
    {
        var sale = CreateSale(currency: "MXN");

        AddLine(sale, quantity: 2m, unitPrice: new Money(10m, "MXN"));

        Assert.Equal(sale.Subtotal, sale.Total);
    }

    [Fact]
    public void AddLineAllowsDecimalQuantity()
    {
        var sale = CreateSale(currency: "MXN");

        AddLine(sale, quantity: 1.5m, unitPrice: new Money(10m, "MXN"));

        Assert.Equal(1.5m, sale.Lines.Single().Quantity);
    }

    [Fact]
    public void AddLineAllowsZeroPrice()
    {
        var sale = CreateSale(currency: "MXN");

        AddLine(sale, unitPrice: new Money(0m, "MXN"));

        Assert.Equal(new Money(0m, "MXN"), sale.Subtotal);
    }

    [Fact]
    public void AddLineAllowsDifferentProducts()
    {
        var sale = CreateSale();

        AddLine(sale, productId: ProductId.New());
        AddLine(sale, productId: ProductId.New());

        Assert.Equal(2, sale.Lines.Count);
    }

    [Fact]
    public void AddLineRejectsDuplicateProductId()
    {
        var sale = CreateSale();
        var productId = ProductId.New();

        AddLine(sale, productId: productId);

        Assert.Throws<DomainValidationException>(() => AddLine(sale, productId: productId));
    }

    [Fact]
    public void AddLineRejectsDuplicateSaleLineId()
    {
        var sale = CreateSale();
        var saleLineId = SaleLineId.New();

        AddLine(sale, saleLineId: saleLineId, productId: ProductId.New());

        Assert.Throws<DomainValidationException>(
            () => AddLine(sale, saleLineId: saleLineId, productId: ProductId.New()));
    }

    [Fact]
    public void AddLineRejectsDifferentCurrency()
    {
        var sale = CreateSale(currency: "MXN");

        Assert.Throws<DomainValidationException>(() => AddLine(sale, unitPrice: new Money(10m, "USD")));
    }

    [Fact]
    public void AddLineRejectsInvalidSaleLineData()
    {
        var sale = CreateSale();

        Assert.Throws<DomainValidationException>(() => AddLine(sale, quantity: 0m));
    }

    // ---------- ChangeLineQuantity ----------

    [Fact]
    public void ChangeLineQuantityUpdatesTheLine()
    {
        var sale = CreateSale();
        var saleLineId = SaleLineId.New();
        AddLine(sale, saleLineId: saleLineId, quantity: 1m, unitPrice: new Money(10m, "MXN"));

        sale.ChangeLineQuantity(saleLineId, 4m);

        Assert.Equal(4m, sale.GetLine(saleLineId).Quantity);
    }

    [Fact]
    public void ChangeLineQuantityRecalculatesSubtotalAndTotal()
    {
        var sale = CreateSale(currency: "MXN");
        var saleLineId = SaleLineId.New();
        AddLine(sale, saleLineId: saleLineId, quantity: 1m, unitPrice: new Money(10m, "MXN"));

        sale.ChangeLineQuantity(saleLineId, 3m);

        Assert.Equal(new Money(30m, "MXN"), sale.Subtotal);
        Assert.Equal(new Money(30m, "MXN"), sale.Total);
    }

    [Fact]
    public void ChangeLineQuantityAllowsDecimalValues()
    {
        var sale = CreateSale();
        var saleLineId = SaleLineId.New();
        AddLine(sale, saleLineId: saleLineId, quantity: 1m, unitPrice: new Money(10m, "MXN"));

        sale.ChangeLineQuantity(saleLineId, 2.5m);

        Assert.Equal(2.5m, sale.GetLine(saleLineId).Quantity);
    }

    [Fact]
    public void ChangeLineQuantityRejectsZero()
    {
        var sale = CreateSale();
        var saleLineId = SaleLineId.New();
        AddLine(sale, saleLineId: saleLineId);

        Assert.Throws<DomainValidationException>(() => sale.ChangeLineQuantity(saleLineId, 0m));
    }

    [Fact]
    public void ChangeLineQuantityRejectsNegative()
    {
        var sale = CreateSale();
        var saleLineId = SaleLineId.New();
        AddLine(sale, saleLineId: saleLineId);

        Assert.Throws<DomainValidationException>(() => sale.ChangeLineQuantity(saleLineId, -1m));
    }

    [Fact]
    public void ChangeLineQuantityRejectsDefaultSaleLineId()
    {
        var sale = CreateSale();
        AddLine(sale);

        Assert.Throws<DomainValidationException>(() => sale.ChangeLineQuantity(default, 1m));
    }

    [Fact]
    public void ChangeLineQuantityRejectsNonExistentLine()
    {
        var sale = CreateSale();
        AddLine(sale);

        Assert.Throws<DomainValidationException>(() => sale.ChangeLineQuantity(SaleLineId.New(), 1m));
    }

    // ---------- RemoveLine ----------

    [Fact]
    public void RemoveLineRemovesAnExistingLine()
    {
        var sale = CreateSale();
        var saleLineId = SaleLineId.New();
        AddLine(sale, saleLineId: saleLineId);

        sale.RemoveLine(saleLineId);

        Assert.Empty(sale.Lines);
    }

    [Fact]
    public void RemoveLineRecalculatesSubtotalAndTotal()
    {
        var sale = CreateSale(currency: "MXN");
        var firstLineId = SaleLineId.New();
        AddLine(sale, saleLineId: firstLineId, productId: ProductId.New(), quantity: 1m, unitPrice: new Money(10m, "MXN"));
        AddLine(sale, productId: ProductId.New(), quantity: 1m, unitPrice: new Money(5m, "MXN"));

        sale.RemoveLine(firstLineId);

        Assert.Equal(new Money(5m, "MXN"), sale.Subtotal);
        Assert.Equal(new Money(5m, "MXN"), sale.Total);
    }

    [Fact]
    public void RemovingLastLineReturnsToZeroWithSameCurrency()
    {
        var sale = CreateSale(currency: "MXN");
        var saleLineId = SaleLineId.New();
        AddLine(sale, saleLineId: saleLineId);

        sale.RemoveLine(saleLineId);

        Assert.Empty(sale.Lines);
        Assert.Equal(new Money(0m, "MXN"), sale.Subtotal);
        Assert.Equal(new Money(0m, "MXN"), sale.Total);
    }

    [Fact]
    public void RemoveLineRejectsDefaultSaleLineId()
    {
        var sale = CreateSale();
        AddLine(sale);

        Assert.Throws<DomainValidationException>(() => sale.RemoveLine(default));
    }

    [Fact]
    public void RemoveLineRejectsNonExistentLine()
    {
        var sale = CreateSale();
        AddLine(sale);

        Assert.Throws<DomainValidationException>(() => sale.RemoveLine(SaleLineId.New()));
    }

    // ---------- Consultas ----------

    [Fact]
    public void ContainsProductReturnsTrueWhenProductExists()
    {
        var sale = CreateSale();
        var productId = ProductId.New();
        AddLine(sale, productId: productId);

        Assert.True(sale.ContainsProduct(productId));
    }

    [Fact]
    public void ContainsProductReturnsFalseWhenProductDoesNotExist()
    {
        var sale = CreateSale();
        AddLine(sale, productId: ProductId.New());

        Assert.False(sale.ContainsProduct(ProductId.New()));
    }

    [Fact]
    public void ContainsProductRejectsDefaultProductId()
    {
        var sale = CreateSale();

        Assert.Throws<DomainValidationException>(() => sale.ContainsProduct(default));
    }

    [Fact]
    public void GetLineReturnsTheLine()
    {
        var sale = CreateSale();
        var saleLineId = SaleLineId.New();
        AddLine(sale, saleLineId: saleLineId);

        var line = sale.GetLine(saleLineId);

        Assert.Equal(saleLineId, line.Id);
    }

    [Fact]
    public void GetLineRejectsDefaultSaleLineId()
    {
        var sale = CreateSale();

        Assert.Throws<DomainValidationException>(() => sale.GetLine(default));
    }

    [Fact]
    public void GetLineRejectsNonExistentLine()
    {
        var sale = CreateSale();

        Assert.Throws<DomainValidationException>(() => sale.GetLine(SaleLineId.New()));
    }

    // ---------- Encapsulación ----------

    [Fact]
    public void LinesCannotBeModifiedExternally()
    {
        var sale = CreateSale();
        var saleLineId = SaleLineId.New();
        AddLine(sale, saleLineId: saleLineId);

        var externalList = (List<SaleLine>)sale.Lines;
        externalList.RemoveAt(0);
        externalList.Clear();
        externalList.Add(new SaleLine(
            SaleLineId.New(),
            ProductId.New(),
            new Sku("SKU-999"),
            "Producto externo",
            1m,
            new Money(1m, "MXN")));

        Assert.Single(sale.Lines);
        Assert.Equal(saleLineId, sale.Lines.Single().Id);
    }

    [Fact]
    public void ModifyingLineObtainedThroughGetLineDoesNotAffectSale()
    {
        var sale = CreateSale(currency: "MXN");
        var saleLineId = SaleLineId.New();
        AddLine(sale, saleLineId: saleLineId, quantity: 1m, unitPrice: new Money(10m, "MXN"));

        var obtainedLine = sale.GetLine(saleLineId);
        obtainedLine.ChangeQuantity(100m);

        var internalLine = sale.GetLine(saleLineId);
        Assert.Equal(1m, internalLine.Quantity);
        Assert.Equal(new Money(10m, "MXN"), sale.Subtotal);
        Assert.Equal(new Money(10m, "MXN"), sale.Total);
    }

    [Fact]
    public void ModifyingLineObtainedThroughLinesDoesNotAffectSale()
    {
        var sale = CreateSale(currency: "MXN");
        var saleLineId = SaleLineId.New();
        AddLine(sale, saleLineId: saleLineId, quantity: 1m, unitPrice: new Money(10m, "MXN"));

        var obtainedLine = sale.Lines.Single();
        obtainedLine.ChangeQuantity(100m);

        var internalLine = sale.GetLine(saleLineId);
        Assert.Equal(1m, internalLine.Quantity);
        Assert.Equal(new Money(10m, "MXN"), sale.Subtotal);
        Assert.Equal(new Money(10m, "MXN"), sale.Total);
    }

    // ---------- Atomicidad ----------

    [Fact]
    public void AddLineWithDifferentCurrencyDoesNotChangeLinesSubtotalOrTotal()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, productId: ProductId.New(), quantity: 1m, unitPrice: new Money(10m, "MXN"));
        var subtotalBefore = sale.Subtotal;
        var totalBefore = sale.Total;
        var lineCountBefore = sale.Lines.Count;

        Assert.Throws<DomainValidationException>(() => AddLine(sale, productId: ProductId.New(), unitPrice: new Money(10m, "USD")));

        Assert.Equal(lineCountBefore, sale.Lines.Count);
        Assert.Equal(subtotalBefore, sale.Subtotal);
        Assert.Equal(totalBefore, sale.Total);
    }

    [Fact]
    public void AddLineWithDuplicateProductIdDoesNotChangeLinesSubtotalOrTotal()
    {
        var sale = CreateSale(currency: "MXN");
        var productId = ProductId.New();
        AddLine(sale, productId: productId, quantity: 1m, unitPrice: new Money(10m, "MXN"));
        var subtotalBefore = sale.Subtotal;
        var totalBefore = sale.Total;
        var lineCountBefore = sale.Lines.Count;

        Assert.Throws<DomainValidationException>(() => AddLine(sale, productId: productId));

        Assert.Equal(lineCountBefore, sale.Lines.Count);
        Assert.Equal(subtotalBefore, sale.Subtotal);
        Assert.Equal(totalBefore, sale.Total);
    }

    [Fact]
    public void AddLineWithDuplicateSaleLineIdDoesNotChangeLinesSubtotalOrTotal()
    {
        var sale = CreateSale(currency: "MXN");
        var saleLineId = SaleLineId.New();
        AddLine(sale, saleLineId: saleLineId, productId: ProductId.New(), quantity: 1m, unitPrice: new Money(10m, "MXN"));
        var subtotalBefore = sale.Subtotal;
        var totalBefore = sale.Total;
        var lineCountBefore = sale.Lines.Count;

        Assert.Throws<DomainValidationException>(
            () => AddLine(sale, saleLineId: saleLineId, productId: ProductId.New()));

        Assert.Equal(lineCountBefore, sale.Lines.Count);
        Assert.Equal(subtotalBefore, sale.Subtotal);
        Assert.Equal(totalBefore, sale.Total);
    }

    [Fact]
    public void InvalidChangeLineQuantityPreservesLineAndTotals()
    {
        var sale = CreateSale(currency: "MXN");
        var saleLineId = SaleLineId.New();
        AddLine(sale, saleLineId: saleLineId, quantity: 3m, unitPrice: new Money(10m, "MXN"));
        var subtotalBefore = sale.Subtotal;
        var totalBefore = sale.Total;

        Assert.Throws<DomainValidationException>(() => sale.ChangeLineQuantity(saleLineId, -1m));

        Assert.Equal(3m, sale.GetLine(saleLineId).Quantity);
        Assert.Equal(subtotalBefore, sale.Subtotal);
        Assert.Equal(totalBefore, sale.Total);
    }

    [Fact]
    public void ChangeLineQuantityOfNonExistentLinePreservesLinesAndTotals()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 3m, unitPrice: new Money(10m, "MXN"));
        var subtotalBefore = sale.Subtotal;
        var totalBefore = sale.Total;
        var lineCountBefore = sale.Lines.Count;

        Assert.Throws<DomainValidationException>(() => sale.ChangeLineQuantity(SaleLineId.New(), 1m));

        Assert.Equal(lineCountBefore, sale.Lines.Count);
        Assert.Equal(subtotalBefore, sale.Subtotal);
        Assert.Equal(totalBefore, sale.Total);
    }

    [Fact]
    public void RemoveNonExistentLinePreservesLinesAndTotals()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 3m, unitPrice: new Money(10m, "MXN"));
        var subtotalBefore = sale.Subtotal;
        var totalBefore = sale.Total;
        var lineCountBefore = sale.Lines.Count;

        Assert.Throws<DomainValidationException>(() => sale.RemoveLine(SaleLineId.New()));

        Assert.Equal(lineCountBefore, sale.Lines.Count);
        Assert.Equal(subtotalBefore, sale.Subtotal);
        Assert.Equal(totalBefore, sale.Total);
    }
}
