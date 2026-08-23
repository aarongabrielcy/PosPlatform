using System.Runtime.Versioning;
using Pos.Application.Receipts;
using Pos.Hardware.Printing;

namespace Pos.Hardware.Tests.Printing;

// Prueba WindowsSpoolReceiptPrinter (sección 44 de la tarea) sin tocar el spooler real de Windows:
// FakeWinSpoolGateway sustituye a Win32SpoolGateway. Cubre: payload/nombre de impresora correctos,
// mapeo de errores, liberación segura de handles y que ninguna excepción escape. La clase se anota
// [SupportedOSPlatform("windows")] en vez de suprimir CA1416, mismo criterio que
// DpapiInstallationCredentialStoreTests en Pos.Infrastructure.Tests.
[SupportedOSPlatform("windows")]
public class WindowsSpoolReceiptPrinterTests
{
    private static readonly FormattedReceipt SampleReceipt = new([0x1B, 0x40, 0x41, 0x42, 0x0A]);

    [Fact]
    public async Task PrintWhenDisabledReturnsNotConfiguredWithoutTouchingGateway()
    {
        var gateway = new FakeWinSpoolGateway();
        var printer = new WindowsSpoolReceiptPrinter(
            new FixedReceiptPrinterOptionsProvider(new ReceiptPrinterOptions { Enabled = false, PrinterName = "TM-T20" }), gateway);

        var outcome = await printer.PrintAsync(SampleReceipt);

        Assert.Equal(PrinterOutcomeStatus.NotConfigured, outcome.Status);
        Assert.Equal(0, gateway.OpenPrinterCallCount);
    }

    [Fact]
    public async Task PrintWhenNoPrinterNameConfiguredReturnsNotConfigured()
    {
        var gateway = new FakeWinSpoolGateway();
        var printer = new WindowsSpoolReceiptPrinter(
            new FixedReceiptPrinterOptionsProvider(new ReceiptPrinterOptions { Enabled = true, PrinterName = "  " }), gateway);

        var outcome = await printer.PrintAsync(SampleReceipt);

        Assert.Equal(PrinterOutcomeStatus.NotConfigured, outcome.Status);
        Assert.Equal(0, gateway.OpenPrinterCallCount);
    }

    [Fact]
    public async Task PrintWhenSuccessfulSendsExactPayloadAndPrinterName()
    {
        var gateway = new FakeWinSpoolGateway();
        var printer = new WindowsSpoolReceiptPrinter(
            new FixedReceiptPrinterOptionsProvider(new ReceiptPrinterOptions { Enabled = true, PrinterName = "TM-T20" }), gateway);

        var outcome = await printer.PrintAsync(SampleReceipt);

        Assert.Equal(PrinterOutcomeStatus.Success, outcome.Status);
        Assert.Equal("TM-T20", gateway.LastPrinterName);
        Assert.Equal(SampleReceipt.Payload.ToArray(), gateway.LastWrittenData);
    }

    [Fact]
    public async Task PrintReleasesHandleEvenOnSuccess()
    {
        var gateway = new FakeWinSpoolGateway();
        var printer = new WindowsSpoolReceiptPrinter(
            new FixedReceiptPrinterOptionsProvider(new ReceiptPrinterOptions { Enabled = true, PrinterName = "TM-T20" }), gateway);

        await printer.PrintAsync(SampleReceipt);

        Assert.Equal(1, gateway.EndPagePrinterCallCount);
        Assert.Equal(1, gateway.EndDocPrinterCallCount);
        Assert.Equal(1, gateway.ClosePrinterCallCount);
    }

    [Fact]
    public async Task PrintWhenOpenPrinterFailsReturnsPrinterUnavailableWithoutClosing()
    {
        var gateway = new FakeWinSpoolGateway { OpenPrinterResult = false };
        var printer = new WindowsSpoolReceiptPrinter(
            new FixedReceiptPrinterOptionsProvider(new ReceiptPrinterOptions { Enabled = true, PrinterName = "Missing" }), gateway);

        var outcome = await printer.PrintAsync(SampleReceipt);

        Assert.Equal(PrinterOutcomeStatus.PrinterUnavailable, outcome.Status);
        Assert.NotNull(outcome.TechnicalDetail);
        Assert.Equal(0, gateway.ClosePrinterCallCount);
    }

    [Fact]
    public async Task PrintWhenStartDocPrinterFailsReturnsPrintFailedAndStillClosesHandle()
    {
        var gateway = new FakeWinSpoolGateway { StartDocPrinterResult = false };
        var printer = new WindowsSpoolReceiptPrinter(
            new FixedReceiptPrinterOptionsProvider(new ReceiptPrinterOptions { Enabled = true, PrinterName = "TM-T20" }), gateway);

        var outcome = await printer.PrintAsync(SampleReceipt);

        Assert.Equal(PrinterOutcomeStatus.PrintFailed, outcome.Status);
        Assert.Equal(1, gateway.ClosePrinterCallCount);
        Assert.Equal(0, gateway.EndDocPrinterCallCount);
    }

    [Fact]
    public async Task PrintWhenWritePrinterFailsReturnsPrintFailedAndClosesEverything()
    {
        var gateway = new FakeWinSpoolGateway { WritePrinterResult = false };
        var printer = new WindowsSpoolReceiptPrinter(
            new FixedReceiptPrinterOptionsProvider(new ReceiptPrinterOptions { Enabled = true, PrinterName = "TM-T20" }), gateway);

        var outcome = await printer.PrintAsync(SampleReceipt);

        Assert.Equal(PrinterOutcomeStatus.PrintFailed, outcome.Status);
        Assert.Equal(1, gateway.EndPagePrinterCallCount);
        Assert.Equal(1, gateway.EndDocPrinterCallCount);
        Assert.Equal(1, gateway.ClosePrinterCallCount);
    }

    [Fact]
    public async Task PrintWhenWritePrinterWritesFewerBytesThanExpectedReturnsPrintFailed()
    {
        var gateway = new FakeWinSpoolGateway { WritePrinterBytesWrittenOverride = 1 };
        var printer = new WindowsSpoolReceiptPrinter(
            new FixedReceiptPrinterOptionsProvider(new ReceiptPrinterOptions { Enabled = true, PrinterName = "TM-T20" }), gateway);

        var outcome = await printer.PrintAsync(SampleReceipt);

        Assert.Equal(PrinterOutcomeStatus.PrintFailed, outcome.Status);
    }

    [Fact]
    public async Task PrintWhenGatewayThrowsNeverLetsExceptionEscapeAndStillClosesHandle()
    {
        var gateway = new FakeWinSpoolGateway { ThrowOnWritePrinter = new InvalidOperationException("boom") };
        var printer = new WindowsSpoolReceiptPrinter(
            new FixedReceiptPrinterOptionsProvider(new ReceiptPrinterOptions { Enabled = true, PrinterName = "TM-T20" }), gateway);

        var outcome = await printer.PrintAsync(SampleReceipt);

        Assert.Equal(PrinterOutcomeStatus.PrintFailed, outcome.Status);
        Assert.Contains("boom", outcome.TechnicalDetail);
        Assert.Equal(1, gateway.ClosePrinterCallCount);
    }

    // BASIC-CFG-01, sección 32/33: un cambio de impresora reflejado en el provider (sin reconstruir
    // WindowsSpoolReceiptPrinter) debe usarse en la siguiente impresión sin reiniciar la aplicación.
    [Fact]
    public async Task PrintUsesTheCurrentPrinterNameFromTheProviderWithoutReconstructingThePrinter()
    {
        var gateway = new FakeWinSpoolGateway();
        var mutableProvider = new MutableReceiptPrinterOptionsProvider(
            new ReceiptPrinterOptions { Enabled = true, PrinterName = "Printer A" });
        var printer = new WindowsSpoolReceiptPrinter(mutableProvider, gateway);

        await printer.PrintAsync(SampleReceipt);
        Assert.Equal("Printer A", gateway.LastPrinterName);

        mutableProvider.Current = new ReceiptPrinterOptions { Enabled = true, PrinterName = "Printer B" };
        await printer.PrintAsync(SampleReceipt);

        Assert.Equal("Printer B", gateway.LastPrinterName);
    }

    private sealed class MutableReceiptPrinterOptionsProvider : IReceiptPrinterOptionsProvider
    {
        public MutableReceiptPrinterOptionsProvider(ReceiptPrinterOptions current) => Current = current;

        public ReceiptPrinterOptions Current { get; set; }

        public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
