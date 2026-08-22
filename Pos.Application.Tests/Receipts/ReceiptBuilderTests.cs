using Pos.Application.Receipts;
using Pos.Application.Sales.History;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Sales;

namespace Pos.Application.Tests.Receipts;

// Prueba que Receipt se construye EXCLUSIVAMENTE a partir del snapshot histórico SaleHistoryDetail
// (sección 36/43 de la tarea): nunca reconsulta Product/precio actual. Como ReceiptBuilder no recibe
// ningún parámetro de Product/catálogo actual, un cambio posterior de nombre/precio del producto
// estructuralmente no puede alcanzar el ticket: la única entrada es el propio snapshot ya persistido.
public sealed class ReceiptBuilderTests
{
    private static readonly Guid SampleSaleId = Guid.Parse("ab34cd12-0000-0000-0000-000000000000");
    private static readonly DateTimeOffset CompletedAtUtc = new(2026, 8, 20, 20, 15, 0, TimeSpan.Zero);

    [Fact]
    public void BuildUsesTheExactHistoricalLineSnapshotEvenIfItDiffersFromWhatAProductLooksLikeToday()
    {
        // Simula una venta completada con "Refresco Retro" a 100.00, el precio/nombre vigentes en el
        // momento de la venta. ReceiptBuilder no recibe ningún dato de Product actual: no hay forma
        // de que un renombramiento/repreciado posterior alcance el Receipt ya construido.
        var detail = BuildDetail(
            lines: [new SaleHistoryDetailLine("SKU-100", "Refresco Retro", 1m, 100.00m, 100.00m, "MXN")]);

        var receipt = ReceiptBuilder.Build(detail, "PosPlatform Demo", isReprint: true, cashTendered: null, changeDue: null);

        var line = Assert.Single(receipt.Lines);
        Assert.Equal("Refresco Retro", line.ProductName);
        Assert.Equal(100.00m, line.UnitPrice);
        Assert.Equal(100.00m, line.LineTotal);
    }

    [Fact]
    public void BuildDerivesSaleReferenceFromTheFirstEightHexCharactersOfSaleIdUppercase()
    {
        var detail = BuildDetail();

        var receipt = ReceiptBuilder.Build(detail, "PosPlatform Demo", isReprint: false, cashTendered: null, changeDue: null);

        Assert.Equal("AB34CD12", receipt.SaleReference);
    }

    [Fact]
    public void BuildCopiesRegisterCashierSubtotalTotalAndCurrencyFromTheDetail()
    {
        var detail = BuildDetail();

        var receipt = ReceiptBuilder.Build(detail, "PosPlatform Demo", isReprint: false, cashTendered: null, changeDue: null);

        Assert.Equal(detail.RegisterName, receipt.RegisterName);
        Assert.Equal(detail.CashierDisplayName, receipt.CashierDisplayName);
        Assert.Equal(detail.Subtotal, receipt.Subtotal);
        Assert.Equal(detail.Total, receipt.Total);
        Assert.Equal(detail.Currency, receipt.Currency);
    }

    [Fact]
    public void BuildForReprintNeverCarriesCashTenderedOrChangeEvenWhenProvidedAsNull()
    {
        var detail = BuildDetail();

        var receipt = ReceiptBuilder.Build(detail, "PosPlatform Demo", isReprint: true, cashTendered: null, changeDue: null);

        Assert.True(receipt.IsReprint);
        Assert.Null(receipt.CashTendered);
        Assert.Null(receipt.ChangeDue);
    }

    [Fact]
    public void BuildForInitialCashPrintCarriesTheProvidedCashTenderedAndChange()
    {
        var detail = BuildDetail();

        var receipt = ReceiptBuilder.Build(detail, "PosPlatform Demo", isReprint: false, cashTendered: 200m, changeDue: 70m);

        Assert.False(receipt.IsReprint);
        Assert.Equal(200m, receipt.CashTendered);
        Assert.Equal(70m, receipt.ChangeDue);
    }

    [Fact]
    public void BuildCopiesPaymentMethodAmountAndReferenceFromTheDetail()
    {
        var detail = BuildDetail(
            payments: [new SaleHistoryDetailPayment(PaymentMethod.Card, 130.00m, "MXN", CompletedAtUtc, "AUTH-5544")]);

        var receipt = ReceiptBuilder.Build(detail, "PosPlatform Demo", isReprint: false, cashTendered: null, changeDue: null);

        var payment = Assert.Single(receipt.Payments);
        Assert.Equal(PaymentMethod.Card, payment.Method);
        Assert.Equal(130.00m, payment.Amount);
        Assert.Equal("AUTH-5544", payment.Reference);
    }

    private static SaleHistoryDetail BuildDetail(
        IReadOnlyList<SaleHistoryDetailLine>? lines = null,
        IReadOnlyList<SaleHistoryDetailPayment>? payments = null) =>
        new(
            new SaleId(SampleSaleId),
            SaleStatus.Completed,
            CompletedAtUtc.AddMinutes(-2),
            CompletedAtUtc,
            UserId.New(),
            "Ana Cajera",
            RegisterId.New(),
            "Caja 1",
            RegisterSessionId.New(),
            130.00m,
            130.00m,
            "MXN",
            lines ?? [new SaleHistoryDetailLine("SKU-001", "Refresco de cola 600ml", 1m, 130.00m, 130.00m, "MXN")],
            payments ?? [new SaleHistoryDetailPayment(PaymentMethod.Cash, 130.00m, "MXN", CompletedAtUtc, null)]);
}
