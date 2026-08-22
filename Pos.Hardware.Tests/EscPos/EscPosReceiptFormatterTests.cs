using Pos.Application.Receipts;
using Pos.Domain.Sales;
using Pos.Hardware.EscPos;

namespace Pos.Hardware.Tests.EscPos;

// Pruebas de contenido textual del ticket (sección 42 de la tarea): se afirman contra
// EscPosReceiptFormatter.BuildTextLines (texto plano) en vez de comparar strings gigantes o bytes
// ESC/POS completos, para que cada aserción sea específica y legible (sección 42: "focused
// assertions"). Las pruebas de comandos binarios/codificación viven en EscPosCommandBytesTests.
public sealed class EscPosReceiptFormatterTests
{
    private static readonly DateTimeOffset CompletedAtUtc = new(2026, 8, 20, 18, 30, 0, TimeSpan.Zero);

    [Fact]
    public void BuildTextLinesIncludesSaleReferenceRegisterAndCashier()
    {
        var receipt = BuildReceipt();

        var lines = EscPosReceiptFormatter.BuildTextLines(receipt, 48);

        Assert.Contains(lines, l => l.Contains("Venta: ABCD1234", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("Caja: Caja 1", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("Cajero: Ana Cajera", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildTextLinesIncludesLocalDateTimeNotRawUtc()
    {
        var receipt = BuildReceipt();

        var lines = EscPosReceiptFormatter.BuildTextLines(receipt, 48);

        var expectedLocal = CompletedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Contains(lines, l => l.Contains(expectedLocal, StringComparison.Ordinal));
    }

    [Fact]
    public void BuildTextLinesIncludesEachLineQuantityUnitPriceAndLineTotal()
    {
        var receipt = BuildReceipt();

        var lines = EscPosReceiptFormatter.BuildTextLines(receipt, 48);

        Assert.Contains(lines, l => l.Contains("Refresco de cola 600ml", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("2 x 15.00", StringComparison.Ordinal) && l.Contains("30.00", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildTextLinesIncludesTotal()
    {
        var receipt = BuildReceipt();

        var lines = EscPosReceiptFormatter.BuildTextLines(receipt, 48);

        Assert.Contains(lines, l => l.TrimStart().StartsWith("TOTAL", StringComparison.Ordinal) && l.Contains("30.00 MXN", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildTextLinesCashPaymentShowsEfectivoLabelAndTenderedChange()
    {
        var receipt = BuildReceipt(cashTendered: 50m, changeDue: 20m);

        var lines = EscPosReceiptFormatter.BuildTextLines(receipt, 48);

        Assert.Contains(lines, l => l.Contains("Efectivo", StringComparison.Ordinal) && l.Contains("30.00 MXN", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("Efectivo recibido", StringComparison.Ordinal) && l.Contains("50.00 MXN", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("Cambio", StringComparison.Ordinal) && l.Contains("20.00 MXN", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildTextLinesReprintWithoutCashTenderedNeverInventsCashFigures()
    {
        var receipt = BuildReceipt(isReprint: true, cashTendered: null, changeDue: null);

        var lines = EscPosReceiptFormatter.BuildTextLines(receipt, 48);

        Assert.DoesNotContain(lines, l => l.Contains("Efectivo recibido", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, l => l.Contains("Cambio", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildTextLinesCardPaymentShowsTarjetaLabelAndReferenceNeverPan()
    {
        var receipt = BuildReceipt(payments: [new ReceiptPayment(PaymentMethod.Card, 30m, "AUTH-778899")]);

        var lines = EscPosReceiptFormatter.BuildTextLines(receipt, 48);

        Assert.Contains(lines, l => l.Contains("Tarjeta", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("Ref: AUTH-778899", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, l => l.Contains("****", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildTextLinesAlwaysIncludesNonCfdiWording()
    {
        var receipt = BuildReceipt();

        var lines = EscPosReceiptFormatter.BuildTextLines(receipt, 48);

        Assert.Contains(lines, l => l.Contains("No es CFDI", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildTextLinesReprintIncludesReimpresionMarker()
    {
        var receipt = BuildReceipt(isReprint: true);

        var lines = EscPosReceiptFormatter.BuildTextLines(receipt, 48);

        Assert.Contains(lines, l => l.Contains("REIMPRESION", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildTextLinesInitialPrintNeverIncludesReimpresionMarker()
    {
        var receipt = BuildReceipt(isReprint: false);

        var lines = EscPosReceiptFormatter.BuildTextLines(receipt, 48);

        Assert.DoesNotContain(lines, l => l.Contains("REIMPRESION", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(32)]
    [InlineData(48)]
    public void BuildTextLinesLongProductNameWrapsWithoutExceedingColumnsAndKeepsTotalIntact(int columns)
    {
        var longName = "Paquete familiar de galletas de chocolate surtidas edicion especial navidad";
        var lines = new[] { new ReceiptLine("SKU-1", longName, 1m, 199.99m, 199.99m) };
        var receipt = BuildReceipt(lines: lines);

        var textLines = EscPosReceiptFormatter.BuildTextLines(receipt, columns);

        Assert.All(textLines, l => Assert.True(l.Length <= columns, $"Línea excede columnas ({l.Length} > {columns}): '{l}'"));
        Assert.Contains(textLines, l => l.Contains("199.99", StringComparison.Ordinal));
        Assert.DoesNotContain(textLines, l => l.Contains("199.9", StringComparison.Ordinal) && !l.Contains("199.99", StringComparison.Ordinal));
    }

    [Fact]
    public void GetColumnsMapsPaperWidthToDocumentedColumnCounts()
    {
        Assert.Equal(32, EscPosReceiptFormatter.GetColumns(ReceiptPaperWidth.Mm58));
        Assert.Equal(48, EscPosReceiptFormatter.GetColumns(ReceiptPaperWidth.Mm80));
    }

    private static Receipt BuildReceipt(
        bool isReprint = false,
        decimal? cashTendered = null,
        decimal? changeDue = null,
        IReadOnlyList<ReceiptLine>? lines = null,
        IReadOnlyList<ReceiptPayment>? payments = null)
    {
        lines ??= [new ReceiptLine("SKU-001", "Refresco de cola 600ml", 2m, 15.00m, 30.00m)];
        payments ??= [new ReceiptPayment(PaymentMethod.Cash, 30.00m, null)];

        return new Receipt(
            "ABCD1234",
            CompletedAtUtc,
            "PosPlatform Demo",
            "Caja 1",
            "Ana Cajera",
            lines,
            30.00m,
            30.00m,
            "MXN",
            payments,
            cashTendered,
            changeDue,
            isReprint);
    }
}
