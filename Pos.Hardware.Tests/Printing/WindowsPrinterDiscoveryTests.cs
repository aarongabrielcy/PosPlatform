using System.Runtime.Versioning;
using Pos.Hardware.Printing;

namespace Pos.Hardware.Tests.Printing;

// BASIC-CFG-01, sección 13/31/44: prueba WindowsPrinterDiscovery sin requerir una impresora física
// ni Windows real, mismo criterio (fake de gateway) que WindowsSpoolReceiptPrinterTests.
[SupportedOSPlatform("windows")]
public class WindowsPrinterDiscoveryTests
{
    [Fact]
    public async Task ReturnsTheNamesReportedByTheGateway()
    {
        var gateway = new FakePrinterEnumerationGateway { Names = ["58mm Bluetooth Printer", "Microsoft Print to PDF"] };
        var discovery = new WindowsPrinterDiscovery(gateway);

        var names = await discovery.GetInstalledPrinterNamesAsync();

        Assert.Equal(["58mm Bluetooth Printer", "Microsoft Print to PDF"], names);
    }

    [Fact]
    public async Task ReturnsAnEmptyListWhenNoPrintersAreInstalled()
    {
        var gateway = new FakePrinterEnumerationGateway { Names = [] };
        var discovery = new WindowsPrinterDiscovery(gateway);

        var names = await discovery.GetInstalledPrinterNamesAsync();

        Assert.Empty(names);
    }

    // Sección 44: "discovery failure handled" - una falla de enumeración nunca debe propagarse como
    // excepción hacia la pantalla de Configuración.
    [Fact]
    public async Task ReturnsAnEmptyListWhenTheGatewayThrowsInsteadOfPropagatingTheException()
    {
        var gateway = new FakePrinterEnumerationGateway { ThrowOnGetInstalledPrinterNames = new InvalidOperationException("boom") };
        var discovery = new WindowsPrinterDiscovery(gateway);

        var names = await discovery.GetInstalledPrinterNamesAsync();

        Assert.Empty(names);
    }
}
