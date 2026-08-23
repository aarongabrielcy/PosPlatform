using Pos.Hardware.Printing;

namespace Pos.Hardware.Tests.Printing;

internal sealed class FakePrinterEnumerationGateway : IPrinterEnumerationGateway
{
    public IReadOnlyList<string> Names { get; set; } = Array.Empty<string>();

    public Exception? ThrowOnGetInstalledPrinterNames { get; set; }

    public IReadOnlyList<string> GetInstalledPrinterNames()
    {
        if (ThrowOnGetInstalledPrinterNames is not null)
        {
            throw ThrowOnGetInstalledPrinterNames;
        }

        return Names;
    }
}
