using Pos.Application.Receipts;

namespace Pos.Desktop.Tests.LocalConfiguration;

internal sealed class FakePrinterDiscovery : IPrinterDiscovery
{
    public IReadOnlyList<string> Names { get; set; } = Array.Empty<string>();

    public int CallCount { get; private set; }

    public Task<IReadOnlyList<string>> GetInstalledPrinterNamesAsync(CancellationToken cancellationToken = default)
    {
        CallCount++;

        return Task.FromResult(Names);
    }
}
