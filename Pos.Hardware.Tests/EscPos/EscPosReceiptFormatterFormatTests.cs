using Pos.Application.Receipts;
using Pos.Domain.Sales;
using Pos.Hardware.EscPos;

namespace Pos.Hardware.Tests.EscPos;

// Pruebas de comando binario (sección 45 de la tarea): init/selección de página de códigos/feed/
// corte. Complementa EscPosReceiptFormatterTests (contenido textual) verificando el payload
// producido por Format(), sin decodificar manualmente cada byte de todo el ticket.
public sealed class EscPosReceiptFormatterFormatTests
{
    private static readonly DateTimeOffset CompletedAtUtc = new(2026, 8, 20, 18, 30, 0, TimeSpan.Zero);

    [Fact]
    public void FormatPayloadStartsWithInitializeAndCodePage850Selection()
    {
        var formatter = new EscPosReceiptFormatter(new ReceiptPrinterOptions());
        var payload = formatter.Format(BuildReceipt()).Payload;

        Assert.Equal(EscPosCommands.Initialize, payload.Take(EscPosCommands.Initialize.Length).ToArray());

        var afterInit = payload.Skip(EscPosCommands.Initialize.Length).Take(EscPosCommands.SelectCodePage850.Length).ToArray();
        Assert.Equal(EscPosCommands.SelectCodePage850, afterInit);
    }

    [Fact]
    public void FormatPayloadEndsWithFeedAndWithoutCutWhenCutPaperDisabled()
    {
        var formatter = new EscPosReceiptFormatter(new ReceiptPrinterOptions { CutPaper = false });
        var payload = formatter.Format(BuildReceipt()).Payload.ToArray();

        Assert.DoesNotContain(GetAllSlices(payload, EscPosCommands.PartialCut.Length), s => s.SequenceEqual(EscPosCommands.PartialCut));
        Assert.Equal(EscPosCommands.FeedLines(3), payload[^EscPosCommands.FeedLines(3).Length..]);
    }

    [Fact]
    public void FormatPayloadEndsWithCutCommandWhenCutPaperEnabled()
    {
        var formatter = new EscPosReceiptFormatter(new ReceiptPrinterOptions { CutPaper = true });
        var payload = formatter.Format(BuildReceipt()).Payload.ToArray();

        Assert.Equal(EscPosCommands.PartialCut, payload[^EscPosCommands.PartialCut.Length..]);
    }

    [Fact]
    public void FormatPayloadContainsEncodedSpanishCharactersFromCashierName()
    {
        var formatter = new EscPosReceiptFormatter(new ReceiptPrinterOptions());
        var receipt = BuildReceipt(cashierDisplayName: "Ñoño Muñoz");
        var payload = formatter.Format(receipt).Payload.ToArray();

        var encodedCashierLine = Cp850SpanishEncoder.Encode("Cajero: Ñoño Muñoz");
        Assert.Contains(GetAllSlices(payload, encodedCashierLine.Length), s => s.SequenceEqual(encodedCashierLine));
    }

    private static IEnumerable<byte[]> GetAllSlices(byte[] source, int length)
    {
        if (length <= 0 || length > source.Length)
        {
            yield break;
        }

        for (var i = 0; i <= source.Length - length; i++)
        {
            yield return source[i..(i + length)];
        }
    }

    private static Receipt BuildReceipt(string cashierDisplayName = "Ana Cajera") => new(
        "ABCD1234",
        CompletedAtUtc,
        "PosPlatform Demo",
        "Caja 1",
        cashierDisplayName,
        [new ReceiptLine("SKU-001", "Refresco de cola 600ml", 2m, 15.00m, 30.00m)],
        30.00m,
        30.00m,
        "MXN",
        [new ReceiptPayment(PaymentMethod.Cash, 30.00m, null)],
        null,
        null,
        isReprint: false);
}
