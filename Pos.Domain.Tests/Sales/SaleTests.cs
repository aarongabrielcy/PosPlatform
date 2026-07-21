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

    // ---------- Estado inicial ampliado ----------

    [Fact]
    public void NewSaleStartsAsDraft()
    {
        var sale = CreateSale();

        Assert.Equal(SaleStatus.Draft, sale.Status);
    }

    [Fact]
    public void NewSaleStartsWithoutPayments()
    {
        var sale = CreateSale();

        Assert.Empty(sale.Payments);
    }

    [Fact]
    public void NewSaleStartsWithZeroPaidAmount()
    {
        var sale = CreateSale(currency: "MXN");

        Assert.Equal(new Money(0m, "MXN"), sale.PaidAmount);
    }

    [Fact]
    public void NewSaleStartsWithBalanceDueEqualToTotal()
    {
        var sale = CreateSale(currency: "MXN");

        Assert.Equal(sale.Total, sale.BalanceDue);
    }

    [Fact]
    public void NewSaleStartsWithZeroChangeDue()
    {
        var sale = CreateSale(currency: "MXN");

        Assert.Equal(new Money(0m, "MXN"), sale.ChangeDue);
    }

    [Fact]
    public void NewSaleStartsWithNullCompletedAtUtc()
    {
        var sale = CreateSale();

        Assert.Null(sale.CompletedAtUtc);
    }

    // ---------- AddPayment ----------

    private static void AddPayment(
        Sale sale,
        PaymentId? paymentId = null,
        PaymentMethod method = PaymentMethod.Cash,
        Money? amount = null,
        DateTimeOffset? paidAtUtc = null) =>
        sale.AddPayment(
            paymentId ?? PaymentId.New(),
            method,
            amount ?? new Money(10m, "MXN"),
            paidAtUtc ?? CreatedAtUtc);

    [Fact]
    public void AddPaymentAddsValidCashPayment()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        var paymentId = PaymentId.New();

        AddPayment(sale, paymentId: paymentId, method: PaymentMethod.Cash, amount: new Money(100m, "MXN"));

        Assert.Single(sale.Payments);
        Assert.Contains(sale.Payments, p => p.Id == paymentId);
    }

    [Fact]
    public void AddPaymentAddsValidCardPayment()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));

        AddPayment(sale, method: PaymentMethod.Card, amount: new Money(100m, "MXN"));

        Assert.Single(sale.Payments);
        Assert.Equal(PaymentMethod.Card, sale.Payments.Single().Method);
    }

    [Fact]
    public void AddPaymentAddsValidBankTransferPayment()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));

        AddPayment(sale, method: PaymentMethod.BankTransfer, amount: new Money(100m, "MXN"));

        Assert.Single(sale.Payments);
        Assert.Equal(PaymentMethod.BankTransfer, sale.Payments.Single().Method);
    }

    [Fact]
    public void AddPaymentAllowsMultiplePayments()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));

        AddPayment(sale, method: PaymentMethod.Card, amount: new Money(60m, "MXN"));
        AddPayment(sale, method: PaymentMethod.Cash, amount: new Money(40m, "MXN"));

        Assert.Equal(2, sale.Payments.Count);
    }

    [Fact]
    public void AddPaymentRecalculatesPaidAmount()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));

        AddPayment(sale, method: PaymentMethod.Card, amount: new Money(60m, "MXN"));
        AddPayment(sale, method: PaymentMethod.Cash, amount: new Money(40m, "MXN"));

        Assert.Equal(new Money(100m, "MXN"), sale.PaidAmount);
    }

    [Fact]
    public void AddPaymentRecalculatesBalanceDue()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));

        AddPayment(sale, method: PaymentMethod.Cash, amount: new Money(60m, "MXN"));

        Assert.Equal(new Money(40m, "MXN"), sale.BalanceDue);
    }

    [Fact]
    public void AddPaymentRecalculatesChangeDue()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));

        AddPayment(sale, method: PaymentMethod.Cash, amount: new Money(120m, "MXN"));

        Assert.Equal(new Money(20m, "MXN"), sale.ChangeDue);
    }

    [Fact]
    public void AddPaymentAllowsCashOverpayment()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));

        AddPayment(sale, method: PaymentMethod.Cash, amount: new Money(120m, "MXN"));

        Assert.Equal(new Money(120m, "MXN"), sale.PaidAmount);
        Assert.Equal(new Money(0m, "MXN"), sale.BalanceDue);
        Assert.Equal(new Money(20m, "MXN"), sale.ChangeDue);
    }

    [Fact]
    public void AddPaymentAllowsMixedPaymentWhereCashCausesOverpayment()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));

        AddPayment(sale, method: PaymentMethod.Card, amount: new Money(60m, "MXN"));
        AddPayment(sale, method: PaymentMethod.Cash, amount: new Money(50m, "MXN"));

        Assert.Equal(new Money(110m, "MXN"), sale.PaidAmount);
        Assert.Equal(new Money(0m, "MXN"), sale.BalanceDue);
        Assert.Equal(new Money(10m, "MXN"), sale.ChangeDue);
    }

    [Fact]
    public void AddPaymentRejectsCardOverpayment()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));

        Assert.Throws<DomainValidationException>(
            () => AddPayment(sale, method: PaymentMethod.Card, amount: new Money(120m, "MXN")));
    }

    [Fact]
    public void AddPaymentRejectsBankTransferOverpayment()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));

        Assert.Throws<DomainValidationException>(
            () => AddPayment(sale, method: PaymentMethod.BankTransfer, amount: new Money(120m, "MXN")));
    }

    [Fact]
    public void AddPaymentRejectsCardOverpaymentAfterExistingCashOverpayment()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        AddPayment(sale, method: PaymentMethod.Cash, amount: new Money(120m, "MXN"));

        Assert.Throws<DomainValidationException>(
            () => AddPayment(sale, method: PaymentMethod.Card, amount: new Money(10m, "MXN")));
    }

    [Fact]
    public void AddPaymentRejectsDifferentCurrency()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));

        Assert.Throws<DomainValidationException>(
            () => AddPayment(sale, amount: new Money(10m, "USD")));
    }

    [Fact]
    public void AddPaymentRejectsDuplicatePaymentId()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        var paymentId = PaymentId.New();
        AddPayment(sale, paymentId: paymentId, amount: new Money(10m, "MXN"));

        Assert.Throws<DomainValidationException>(
            () => AddPayment(sale, paymentId: paymentId, amount: new Money(10m, "MXN")));
    }

    [Fact]
    public void AddPaymentRejectsInvalidData()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));

        Assert.Throws<DomainValidationException>(
            () => AddPayment(sale, amount: new Money(0m, "MXN")));
    }

    [Fact]
    public void AddPaymentRejectsUndefinedEnumValue()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));

        Assert.Throws<DomainValidationException>(
            () => sale.AddPayment(PaymentId.New(), (PaymentMethod)999, new Money(10m, "MXN"), CreatedAtUtc));
    }

    // ---------- RemovePayment ----------

    [Fact]
    public void RemovePaymentRemovesAnExistingPayment()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        var paymentId = PaymentId.New();
        AddPayment(sale, paymentId: paymentId, amount: new Money(50m, "MXN"));

        sale.RemovePayment(paymentId);

        Assert.Empty(sale.Payments);
    }

    [Fact]
    public void RemovePaymentRecalculatesTotals()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        var paymentId = PaymentId.New();
        AddPayment(sale, paymentId: paymentId, amount: new Money(50m, "MXN"));

        sale.RemovePayment(paymentId);

        Assert.Equal(new Money(0m, "MXN"), sale.PaidAmount);
        Assert.Equal(new Money(100m, "MXN"), sale.BalanceDue);
        Assert.Equal(new Money(0m, "MXN"), sale.ChangeDue);
    }

    [Fact]
    public void RemovingLastPaymentReturnsPaidAmountToZero()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        var paymentId = PaymentId.New();
        AddPayment(sale, paymentId: paymentId, amount: new Money(100m, "MXN"));

        sale.RemovePayment(paymentId);

        Assert.Equal(new Money(0m, "MXN"), sale.PaidAmount);
        Assert.Equal(sale.Total, sale.BalanceDue);
    }

    [Fact]
    public void RemovePaymentRejectsDefaultPaymentId()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        AddPayment(sale);

        Assert.Throws<DomainValidationException>(() => sale.RemovePayment(default));
    }

    [Fact]
    public void RemovePaymentRejectsNonExistentPayment()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        AddPayment(sale);

        Assert.Throws<DomainValidationException>(() => sale.RemovePayment(PaymentId.New()));
    }

    // ---------- Complete ----------

    [Fact]
    public void CompleteCompletesSaleWithExactPayment()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        AddPayment(sale, method: PaymentMethod.Cash, amount: new Money(100m, "MXN"));

        sale.Complete(CreatedAtUtc);

        Assert.Equal(SaleStatus.Completed, sale.Status);
    }

    [Fact]
    public void CompleteCompletesSaleWithCashOverpayment()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        AddPayment(sale, method: PaymentMethod.Cash, amount: new Money(120m, "MXN"));

        sale.Complete(CreatedAtUtc);

        Assert.Equal(SaleStatus.Completed, sale.Status);
        Assert.Equal(new Money(20m, "MXN"), sale.ChangeDue);
    }

    [Fact]
    public void CompleteStoresCompletedAtUtc()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        AddPayment(sale, method: PaymentMethod.Cash, amount: new Money(100m, "MXN"));
        var completedAtUtc = CreatedAtUtc.AddMinutes(5);

        sale.Complete(completedAtUtc);

        Assert.Equal(completedAtUtc, sale.CompletedAtUtc);
    }

    [Fact]
    public void CompleteAllowsCompletedAtUtcEqualToCreatedAtUtc()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        AddPayment(sale, method: PaymentMethod.Cash, amount: new Money(100m, "MXN"));

        sale.Complete(CreatedAtUtc);

        Assert.Equal(CreatedAtUtc, sale.CompletedAtUtc);
    }

    [Fact]
    public void CompleteRejectsSaleWithoutLines()
    {
        var sale = CreateSale(currency: "MXN");

        Assert.Throws<DomainValidationException>(() => sale.Complete(CreatedAtUtc));
    }

    [Fact]
    public void CompleteRejectsZeroTotal()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, unitPrice: new Money(0m, "MXN"));

        Assert.Throws<DomainValidationException>(() => sale.Complete(CreatedAtUtc));
    }

    [Fact]
    public void CompleteRejectsInsufficientPayment()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        AddPayment(sale, method: PaymentMethod.Cash, amount: new Money(50m, "MXN"));

        Assert.Throws<DomainValidationException>(() => sale.Complete(CreatedAtUtc));
    }

    [Fact]
    public void CompleteRejectsNonUtcDate()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        AddPayment(sale, method: PaymentMethod.Cash, amount: new Money(100m, "MXN"));
        var nonUtc = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.FromHours(-5));

        Assert.Throws<DomainValidationException>(() => sale.Complete(nonUtc));
    }

    [Fact]
    public void CompleteRejectsDateBeforeCreatedAtUtc()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        AddPayment(sale, method: PaymentMethod.Cash, amount: new Money(100m, "MXN"));
        var beforeCreation = CreatedAtUtc.AddMinutes(-1);

        Assert.Throws<DomainValidationException>(() => sale.Complete(beforeCreation));
    }

    [Fact]
    public void CompleteRejectsCompletingTwice()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        AddPayment(sale, method: PaymentMethod.Cash, amount: new Money(100m, "MXN"));
        sale.Complete(CreatedAtUtc);

        Assert.Throws<DomainValidationException>(() => sale.Complete(CreatedAtUtc));
    }

    // ---------- Bloqueo posterior a Completed ----------

    [Fact]
    public void CompletedSaleRejectsAddLine()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        AddPayment(sale, method: PaymentMethod.Cash, amount: new Money(100m, "MXN"));
        sale.Complete(CreatedAtUtc);
        var lineCountBefore = sale.Lines.Count;
        var totalBefore = sale.Total;

        Assert.Throws<DomainValidationException>(() => AddLine(sale, productId: ProductId.New()));

        Assert.Equal(lineCountBefore, sale.Lines.Count);
        Assert.Equal(totalBefore, sale.Total);
    }

    [Fact]
    public void CompletedSaleRejectsChangeLineQuantity()
    {
        var sale = CreateSale(currency: "MXN");
        var saleLineId = SaleLineId.New();
        AddLine(sale, saleLineId: saleLineId, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        AddPayment(sale, method: PaymentMethod.Cash, amount: new Money(100m, "MXN"));
        sale.Complete(CreatedAtUtc);

        Assert.Throws<DomainValidationException>(() => sale.ChangeLineQuantity(saleLineId, 5m));

        Assert.Equal(10m, sale.GetLine(saleLineId).Quantity);
    }

    [Fact]
    public void CompletedSaleRejectsRemoveLine()
    {
        var sale = CreateSale(currency: "MXN");
        var saleLineId = SaleLineId.New();
        AddLine(sale, saleLineId: saleLineId, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        AddPayment(sale, method: PaymentMethod.Cash, amount: new Money(100m, "MXN"));
        sale.Complete(CreatedAtUtc);

        Assert.Throws<DomainValidationException>(() => sale.RemoveLine(saleLineId));

        Assert.Single(sale.Lines);
    }

    [Fact]
    public void CompletedSaleRejectsAddPayment()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        AddPayment(sale, method: PaymentMethod.Cash, amount: new Money(100m, "MXN"));
        sale.Complete(CreatedAtUtc);
        var paymentCountBefore = sale.Payments.Count;

        Assert.Throws<DomainValidationException>(
            () => AddPayment(sale, method: PaymentMethod.Card, amount: new Money(10m, "MXN")));

        Assert.Equal(paymentCountBefore, sale.Payments.Count);
    }

    [Fact]
    public void CompletedSaleRejectsRemovePayment()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        var paymentId = PaymentId.New();
        AddPayment(sale, paymentId: paymentId, method: PaymentMethod.Cash, amount: new Money(100m, "MXN"));
        sale.Complete(CreatedAtUtc);

        Assert.Throws<DomainValidationException>(() => sale.RemovePayment(paymentId));

        Assert.Single(sale.Payments);
    }

    // ---------- Encapsulación de Payments ----------

    [Fact]
    public void PaymentsCannotBeModifiedExternally()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        var paymentId = PaymentId.New();
        AddPayment(sale, paymentId: paymentId, amount: new Money(50m, "MXN"));

        var externalList = (List<Payment>)sale.Payments;
        externalList.Clear();
        externalList.Add(new Payment(
            PaymentId.New(), PaymentMethod.Cash, new Money(999m, "MXN"), CreatedAtUtc));

        Assert.Single(sale.Payments);
        Assert.Equal(paymentId, sale.Payments.Single().Id);
    }

    // ---------- Atomicidad de pagos y finalización ----------

    [Fact]
    public void AddPaymentWithDifferentCurrencyDoesNotChangePaymentsOrTotals()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        var paymentCountBefore = sale.Payments.Count;
        var paidBefore = sale.PaidAmount;
        var balanceBefore = sale.BalanceDue;
        var changeBefore = sale.ChangeDue;

        Assert.Throws<DomainValidationException>(() => AddPayment(sale, amount: new Money(10m, "USD")));

        Assert.Equal(paymentCountBefore, sale.Payments.Count);
        Assert.Equal(paidBefore, sale.PaidAmount);
        Assert.Equal(balanceBefore, sale.BalanceDue);
        Assert.Equal(changeBefore, sale.ChangeDue);
        Assert.Equal(SaleStatus.Draft, sale.Status);
        Assert.Null(sale.CompletedAtUtc);
    }

    [Fact]
    public void AddPaymentWithDuplicateIdDoesNotChangePaymentsOrTotals()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        var paymentId = PaymentId.New();
        AddPayment(sale, paymentId: paymentId, amount: new Money(50m, "MXN"));
        var paymentCountBefore = sale.Payments.Count;
        var paidBefore = sale.PaidAmount;
        var balanceBefore = sale.BalanceDue;
        var changeBefore = sale.ChangeDue;

        Assert.Throws<DomainValidationException>(
            () => AddPayment(sale, paymentId: paymentId, amount: new Money(10m, "MXN")));

        Assert.Equal(paymentCountBefore, sale.Payments.Count);
        Assert.Equal(paidBefore, sale.PaidAmount);
        Assert.Equal(balanceBefore, sale.BalanceDue);
        Assert.Equal(changeBefore, sale.ChangeDue);
        Assert.Equal(SaleStatus.Draft, sale.Status);
        Assert.Null(sale.CompletedAtUtc);
    }

    [Fact]
    public void AddPaymentCardOverpaymentDoesNotChangePaymentsOrTotals()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        var paymentCountBefore = sale.Payments.Count;
        var paidBefore = sale.PaidAmount;
        var balanceBefore = sale.BalanceDue;
        var changeBefore = sale.ChangeDue;

        Assert.Throws<DomainValidationException>(
            () => AddPayment(sale, method: PaymentMethod.Card, amount: new Money(120m, "MXN")));

        Assert.Equal(paymentCountBefore, sale.Payments.Count);
        Assert.Equal(paidBefore, sale.PaidAmount);
        Assert.Equal(balanceBefore, sale.BalanceDue);
        Assert.Equal(changeBefore, sale.ChangeDue);
        Assert.Equal(SaleStatus.Draft, sale.Status);
        Assert.Null(sale.CompletedAtUtc);
    }

    [Fact]
    public void AddPaymentBankTransferOverpaymentDoesNotChangePaymentsOrTotals()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        var paymentCountBefore = sale.Payments.Count;
        var paidBefore = sale.PaidAmount;
        var balanceBefore = sale.BalanceDue;
        var changeBefore = sale.ChangeDue;

        Assert.Throws<DomainValidationException>(
            () => AddPayment(sale, method: PaymentMethod.BankTransfer, amount: new Money(120m, "MXN")));

        Assert.Equal(paymentCountBefore, sale.Payments.Count);
        Assert.Equal(paidBefore, sale.PaidAmount);
        Assert.Equal(balanceBefore, sale.BalanceDue);
        Assert.Equal(changeBefore, sale.ChangeDue);
        Assert.Equal(SaleStatus.Draft, sale.Status);
        Assert.Null(sale.CompletedAtUtc);
    }

    [Fact]
    public void RemoveNonExistentPaymentDoesNotChangePaymentsOrTotals()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        AddPayment(sale, amount: new Money(50m, "MXN"));
        var paymentCountBefore = sale.Payments.Count;
        var paidBefore = sale.PaidAmount;
        var balanceBefore = sale.BalanceDue;
        var changeBefore = sale.ChangeDue;

        Assert.Throws<DomainValidationException>(() => sale.RemovePayment(PaymentId.New()));

        Assert.Equal(paymentCountBefore, sale.Payments.Count);
        Assert.Equal(paidBefore, sale.PaidAmount);
        Assert.Equal(balanceBefore, sale.BalanceDue);
        Assert.Equal(changeBefore, sale.ChangeDue);
        Assert.Equal(SaleStatus.Draft, sale.Status);
        Assert.Null(sale.CompletedAtUtc);
    }

    [Fact]
    public void CompleteWithInsufficientPaymentDoesNotChangeStatus()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        AddPayment(sale, amount: new Money(50m, "MXN"));

        Assert.Throws<DomainValidationException>(() => sale.Complete(CreatedAtUtc));

        Assert.Equal(SaleStatus.Draft, sale.Status);
        Assert.Null(sale.CompletedAtUtc);
    }

    [Fact]
    public void LineOperationAfterCompletedDoesNotChangeLinesOrTotals()
    {
        var sale = CreateSale(currency: "MXN");
        var saleLineId = SaleLineId.New();
        AddLine(sale, saleLineId: saleLineId, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        AddPayment(sale, amount: new Money(100m, "MXN"));
        sale.Complete(CreatedAtUtc);
        var totalBefore = sale.Total;
        var lineCountBefore = sale.Lines.Count;

        Assert.Throws<DomainValidationException>(() => sale.ChangeLineQuantity(saleLineId, 20m));

        Assert.Equal(lineCountBefore, sale.Lines.Count);
        Assert.Equal(totalBefore, sale.Total);
        Assert.Equal(SaleStatus.Completed, sale.Status);
    }

    // ---------- Interacción entre líneas y pagos ----------

    [Fact]
    public void IncreasingLineAfterExactCashPaymentRecalculatesBalanceDue()
    {
        var sale = CreateSale(currency: "MXN");
        var saleLineId = SaleLineId.New();
        AddLine(sale, saleLineId: saleLineId, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        AddPayment(sale, method: PaymentMethod.Cash, amount: new Money(100m, "MXN"));

        sale.ChangeLineQuantity(saleLineId, 15m);

        Assert.Equal(new Money(150m, "MXN"), sale.Total);
        Assert.Equal(new Money(100m, "MXN"), sale.PaidAmount);
        Assert.Equal(new Money(50m, "MXN"), sale.BalanceDue);
        Assert.Equal(new Money(0m, "MXN"), sale.ChangeDue);
    }

    [Fact]
    public void IncreasingLineAfterCashOverpaymentRecalculatesChangeDue()
    {
        var sale = CreateSale(currency: "MXN");
        var saleLineId = SaleLineId.New();
        AddLine(sale, saleLineId: saleLineId, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        AddPayment(sale, method: PaymentMethod.Cash, amount: new Money(120m, "MXN"));
        Assert.Equal(new Money(20m, "MXN"), sale.ChangeDue);

        sale.ChangeLineQuantity(saleLineId, 15m);

        Assert.Equal(new Money(150m, "MXN"), sale.Total);
        Assert.Equal(new Money(120m, "MXN"), sale.PaidAmount);
        Assert.Equal(new Money(30m, "MXN"), sale.BalanceDue);
        Assert.Equal(new Money(0m, "MXN"), sale.ChangeDue);
    }

    [Fact]
    public void ReducingLineAfterCashOverpaymentRecalculatesChangeDue()
    {
        var sale = CreateSale(currency: "MXN");
        var saleLineId = SaleLineId.New();
        AddLine(sale, saleLineId: saleLineId, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        AddPayment(sale, method: PaymentMethod.Cash, amount: new Money(120m, "MXN"));

        sale.ChangeLineQuantity(saleLineId, 5m);

        Assert.Equal(new Money(50m, "MXN"), sale.Total);
        Assert.Equal(new Money(120m, "MXN"), sale.PaidAmount);
        Assert.Equal(new Money(0m, "MXN"), sale.BalanceDue);
        Assert.Equal(new Money(70m, "MXN"), sale.ChangeDue);
    }

    // ---------- Protección de pagos no efectivos ante cambios de línea ----------

    [Fact]
    public void ChangeLineQuantityRejectsReductionBelowExactCardPayment()
    {
        var sale = CreateSale(currency: "MXN");
        var saleLineId = SaleLineId.New();
        AddLine(sale, saleLineId: saleLineId, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        AddPayment(sale, method: PaymentMethod.Card, amount: new Money(100m, "MXN"));
        var subtotalBefore = sale.Subtotal;
        var totalBefore = sale.Total;
        var paidBefore = sale.PaidAmount;
        var balanceBefore = sale.BalanceDue;
        var changeBefore = sale.ChangeDue;

        Assert.Throws<DomainValidationException>(() => sale.ChangeLineQuantity(saleLineId, 5m));

        Assert.Equal(10m, sale.GetLine(saleLineId).Quantity);
        Assert.Equal(subtotalBefore, sale.Subtotal);
        Assert.Equal(totalBefore, sale.Total);
        Assert.Equal(paidBefore, sale.PaidAmount);
        Assert.Equal(balanceBefore, sale.BalanceDue);
        Assert.Equal(changeBefore, sale.ChangeDue);
    }

    [Fact]
    public void ChangeLineQuantityRejectsReductionBelowExactBankTransferPayment()
    {
        var sale = CreateSale(currency: "MXN");
        var saleLineId = SaleLineId.New();
        AddLine(sale, saleLineId: saleLineId, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        AddPayment(sale, method: PaymentMethod.BankTransfer, amount: new Money(100m, "MXN"));
        var subtotalBefore = sale.Subtotal;
        var totalBefore = sale.Total;
        var paidBefore = sale.PaidAmount;
        var balanceBefore = sale.BalanceDue;
        var changeBefore = sale.ChangeDue;

        Assert.Throws<DomainValidationException>(() => sale.ChangeLineQuantity(saleLineId, 5m));

        Assert.Equal(10m, sale.GetLine(saleLineId).Quantity);
        Assert.Equal(subtotalBefore, sale.Subtotal);
        Assert.Equal(totalBefore, sale.Total);
        Assert.Equal(paidBefore, sale.PaidAmount);
        Assert.Equal(balanceBefore, sale.BalanceDue);
        Assert.Equal(changeBefore, sale.ChangeDue);
    }

    [Fact]
    public void RemoveLineRejectsWhenItWouldLeaveTotalBelowExactCardPayment()
    {
        var sale = CreateSale(currency: "MXN");
        var saleLineId = SaleLineId.New();
        AddLine(sale, saleLineId: saleLineId, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        AddPayment(sale, method: PaymentMethod.Card, amount: new Money(100m, "MXN"));
        var lineCountBefore = sale.Lines.Count;
        var totalBefore = sale.Total;
        var paidBefore = sale.PaidAmount;
        var balanceBefore = sale.BalanceDue;
        var changeBefore = sale.ChangeDue;

        Assert.Throws<DomainValidationException>(() => sale.RemoveLine(saleLineId));

        Assert.Equal(lineCountBefore, sale.Lines.Count);
        Assert.Equal(totalBefore, sale.Total);
        Assert.Equal(paidBefore, sale.PaidAmount);
        Assert.Equal(balanceBefore, sale.BalanceDue);
        Assert.Equal(changeBefore, sale.ChangeDue);
    }

    [Fact]
    public void ChangeLineQuantityAllowsValidMixOfCardAndCash()
    {
        var sale = CreateSale(currency: "MXN");
        var saleLineId = SaleLineId.New();
        AddLine(sale, saleLineId: saleLineId, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        AddPayment(sale, method: PaymentMethod.Card, amount: new Money(60m, "MXN"));
        AddPayment(sale, method: PaymentMethod.Cash, amount: new Money(60m, "MXN"));

        sale.ChangeLineQuantity(saleLineId, 8m);

        Assert.Equal(new Money(80m, "MXN"), sale.Total);
        Assert.Equal(new Money(120m, "MXN"), sale.PaidAmount);
        Assert.Equal(new Money(0m, "MXN"), sale.BalanceDue);
        Assert.Equal(new Money(40m, "MXN"), sale.ChangeDue);
    }

    [Fact]
    public void ChangeLineQuantityRejectsInvalidMixWhenCardExceedsProjectedTotal()
    {
        var sale = CreateSale(currency: "MXN");
        var saleLineId = SaleLineId.New();
        AddLine(sale, saleLineId: saleLineId, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        AddPayment(sale, method: PaymentMethod.Card, amount: new Money(90m, "MXN"));
        AddPayment(sale, method: PaymentMethod.Cash, amount: new Money(30m, "MXN"));
        var lineCountBefore = sale.Lines.Count;
        var quantityBefore = sale.GetLine(saleLineId).Quantity;
        var subtotalBefore = sale.Subtotal;
        var totalBefore = sale.Total;
        var paidBefore = sale.PaidAmount;
        var balanceBefore = sale.BalanceDue;
        var changeBefore = sale.ChangeDue;

        Assert.Throws<DomainValidationException>(() => sale.ChangeLineQuantity(saleLineId, 8m));

        Assert.Equal(lineCountBefore, sale.Lines.Count);
        Assert.Equal(quantityBefore, sale.GetLine(saleLineId).Quantity);
        Assert.Equal(subtotalBefore, sale.Subtotal);
        Assert.Equal(totalBefore, sale.Total);
        Assert.Equal(paidBefore, sale.PaidAmount);
        Assert.Equal(balanceBefore, sale.BalanceDue);
        Assert.Equal(changeBefore, sale.ChangeDue);
    }

    [Fact]
    public void ChangeLineQuantityAllowsReductionBelowExactCashPayment()
    {
        var sale = CreateSale(currency: "MXN");
        var saleLineId = SaleLineId.New();
        AddLine(sale, saleLineId: saleLineId, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        AddPayment(sale, method: PaymentMethod.Cash, amount: new Money(100m, "MXN"));

        sale.ChangeLineQuantity(saleLineId, 5m);

        Assert.Equal(new Money(50m, "MXN"), sale.Total);
        Assert.Equal(new Money(100m, "MXN"), sale.PaidAmount);
        Assert.Equal(new Money(0m, "MXN"), sale.BalanceDue);
        Assert.Equal(new Money(50m, "MXN"), sale.ChangeDue);
    }

    [Fact]
    public void ChangeLineQuantityAllowsIncreaseAfterExactCardPayment()
    {
        var sale = CreateSale(currency: "MXN");
        var saleLineId = SaleLineId.New();
        AddLine(sale, saleLineId: saleLineId, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        AddPayment(sale, method: PaymentMethod.Card, amount: new Money(100m, "MXN"));

        sale.ChangeLineQuantity(saleLineId, 15m);

        Assert.Equal(new Money(150m, "MXN"), sale.Total);
        Assert.Equal(new Money(100m, "MXN"), sale.PaidAmount);
        Assert.Equal(new Money(50m, "MXN"), sale.BalanceDue);
        Assert.Equal(new Money(0m, "MXN"), sale.ChangeDue);
    }

    [Fact]
    public void CompleteRejectsSaleWithPositiveBalanceDueAndDoesNotChangeStatus()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        AddPayment(sale, method: PaymentMethod.Cash, amount: new Money(50m, "MXN"));

        Assert.Throws<DomainValidationException>(() => sale.Complete(CreatedAtUtc));

        Assert.Equal(SaleStatus.Draft, sale.Status);
        Assert.Null(sale.CompletedAtUtc);
    }

    // ---------- EnsureCanComplete ----------

    [Fact]
    public void EnsureCanCompleteDoesNotChangeStatusOfAValidSale()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        AddPayment(sale, method: PaymentMethod.Cash, amount: new Money(100m, "MXN"));

        sale.EnsureCanComplete(CreatedAtUtc);

        Assert.Equal(SaleStatus.Draft, sale.Status);
    }

    [Fact]
    public void EnsureCanCompleteDoesNotAssignCompletedAtUtc()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        AddPayment(sale, method: PaymentMethod.Cash, amount: new Money(100m, "MXN"));

        sale.EnsureCanComplete(CreatedAtUtc);

        Assert.Null(sale.CompletedAtUtc);
    }

    [Fact]
    public void EnsureCanCompleteAllowsDateEqualToCreatedAtUtc()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        AddPayment(sale, method: PaymentMethod.Cash, amount: new Money(100m, "MXN"));

        var exception = Record.Exception(() => sale.EnsureCanComplete(CreatedAtUtc));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureCanCompleteRejectsCompletedSale()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        AddPayment(sale, method: PaymentMethod.Cash, amount: new Money(100m, "MXN"));
        sale.Complete(CreatedAtUtc);

        Assert.Throws<DomainValidationException>(() => sale.EnsureCanComplete(CreatedAtUtc));
    }

    [Fact]
    public void EnsureCanCompleteRejectsSaleWithoutLines()
    {
        var sale = CreateSale(currency: "MXN");

        Assert.Throws<DomainValidationException>(() => sale.EnsureCanComplete(CreatedAtUtc));
    }

    [Fact]
    public void EnsureCanCompleteRejectsZeroTotal()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, unitPrice: new Money(0m, "MXN"));

        Assert.Throws<DomainValidationException>(() => sale.EnsureCanComplete(CreatedAtUtc));
    }

    [Fact]
    public void EnsureCanCompleteRejectsInsufficientPayment()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        AddPayment(sale, method: PaymentMethod.Cash, amount: new Money(50m, "MXN"));

        Assert.Throws<DomainValidationException>(() => sale.EnsureCanComplete(CreatedAtUtc));
    }

    [Fact]
    public void EnsureCanCompleteRejectsPositiveBalanceDue()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        AddPayment(sale, method: PaymentMethod.Cash, amount: new Money(50m, "MXN"));

        Assert.Throws<DomainValidationException>(() => sale.EnsureCanComplete(CreatedAtUtc));

        Assert.Equal(SaleStatus.Draft, sale.Status);
        Assert.Null(sale.CompletedAtUtc);
    }

    [Fact]
    public void EnsureCanCompleteRejectsNonUtcDate()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        AddPayment(sale, method: PaymentMethod.Cash, amount: new Money(100m, "MXN"));
        var nonUtc = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.FromHours(-5));

        Assert.Throws<DomainValidationException>(() => sale.EnsureCanComplete(nonUtc));
    }

    [Fact]
    public void EnsureCanCompleteRejectsDateBeforeCreatedAtUtc()
    {
        var sale = CreateSale(currency: "MXN");
        AddLine(sale, quantity: 10m, unitPrice: new Money(10m, "MXN"));
        AddPayment(sale, method: PaymentMethod.Cash, amount: new Money(100m, "MXN"));
        var beforeCreation = CreatedAtUtc.AddMinutes(-1);

        Assert.Throws<DomainValidationException>(() => sale.EnsureCanComplete(beforeCreation));
    }
}
