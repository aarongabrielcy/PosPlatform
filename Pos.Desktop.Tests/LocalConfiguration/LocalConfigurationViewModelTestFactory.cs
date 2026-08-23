using System.IO;
using Pos.Application.Receipts;

namespace Pos.Desktop.Tests.LocalConfiguration;

// Fábrica compartida para pruebas fuera de este namespace (MainWindowViewModelTests/MainWindowTests)
// que solo necesitan que el shell pueda construirse con una instancia válida de
// LocalConfigurationViewModel, sin ejercer ninguno de sus comportamientos.
internal static class LocalConfigurationViewModelTestFactory
{
    public static Pos.Desktop.LocalConfiguration.LocalConfigurationViewModel CreateDefault() =>
        new(
            new FakeLocalSettingsStore(),
            new FakeLocalSettingsService(),
            new FixedReceiptPrinterOptionsProvider(new ReceiptPrinterOptions()),
            new FakePrinterDiscovery(),
            new FakeReceiptPrintingService(),
            new FakeCurrentUserSession(),
            new FakeOrganizationRepository(),
            new FakeApplicationVersionProvider(),
            new FakeApplicationPathProvider(Path.Combine(Path.GetTempPath(), "PosPlatformLocalConfigDefault_" + Guid.NewGuid())),
            new FakeInstallationActivationStateService());
}
