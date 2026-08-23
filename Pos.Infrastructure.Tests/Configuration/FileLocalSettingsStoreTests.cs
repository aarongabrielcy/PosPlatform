using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Configuration;
using Pos.Application.Receipts;
using Pos.Infrastructure.Configuration;
using Pos.Infrastructure.Storage;

namespace Pos.Infrastructure.Tests.Configuration;

public class FileLocalSettingsStoreTests
{
    [Fact]
    public async Task LoadReturnsNullWhenNothingWasSaved()
    {
        var store = CreateStore(out var cleanup);
        try
        {
            Assert.Null(await store.LoadAsync());
        }
        finally
        {
            cleanup();
        }
    }

    [Fact]
    public async Task SaveThenLoadRoundTripsReceiptPrinterAndCashDrawerValues()
    {
        var store = CreateStore(out var cleanup);
        try
        {
            var settings = new LocalSettings
            {
                ReceiptPrinter = new ReceiptPrinterOptions
                {
                    Enabled = true,
                    PrinterName = "58mm Bluetooth Printer",
                    PaperWidth = ReceiptPaperWidth.Mm58,
                    AutoPrint = false,
                    CutPaper = true,
                },
                CashDrawer = new LocalCashDrawerSettings
                {
                    Enabled = true,
                    OpenOnRegisterOpen = true,
                    ConnectionMode = CashDrawerConnectionMode.ViaReceiptPrinter,
                },
            };

            var saved = await store.SaveAsync(settings);
            var loaded = await store.LoadAsync();

            Assert.True(saved);
            Assert.NotNull(loaded);
            Assert.True(loaded!.ReceiptPrinter.Enabled);
            Assert.Equal("58mm Bluetooth Printer", loaded.ReceiptPrinter.PrinterName);
            Assert.Equal(ReceiptPaperWidth.Mm58, loaded.ReceiptPrinter.PaperWidth);
            Assert.False(loaded.ReceiptPrinter.AutoPrint);
            Assert.True(loaded.ReceiptPrinter.CutPaper);
            Assert.True(loaded.CashDrawer.Enabled);
            Assert.True(loaded.CashDrawer.OpenOnRegisterOpen);
        }
        finally
        {
            cleanup();
        }
    }

    [Fact]
    public async Task SecondSaveAtomicallyReplacesThePreviouslyStoredSettings()
    {
        var store = CreateStore(out var cleanup);
        try
        {
            await store.SaveAsync(new LocalSettings { ReceiptPrinter = new ReceiptPrinterOptions { PrinterName = "Printer A" } });
            await store.SaveAsync(new LocalSettings { ReceiptPrinter = new ReceiptPrinterOptions { PrinterName = "Printer B" } });

            var loaded = await store.LoadAsync();

            Assert.Equal("Printer B", loaded!.ReceiptPrinter.PrinterName);
        }
        finally
        {
            cleanup();
        }
    }

    [Fact]
    public async Task SaveNeverLeavesATemporaryFileBehind()
    {
        var root = Path.Combine(Path.GetTempPath(), "PosPlatformLocalSettingsStoreTests_" + Guid.NewGuid());
        var pathProvider = new ApplicationPathProvider(root);
        var store = new FileLocalSettingsStore(pathProvider, NullLogger<FileLocalSettingsStore>.Instance);

        try
        {
            await store.SaveAsync(new LocalSettings { ReceiptPrinter = new ReceiptPrinterOptions { PrinterName = "Printer A" } });
            await store.SaveAsync(new LocalSettings { ReceiptPrinter = new ReceiptPrinterOptions { PrinterName = "Printer B" } });

            var configDirectory = Path.Combine(pathProvider.DataDirectory, "Config");
            var files = Directory.GetFiles(configDirectory);

            Assert.Single(files);
            Assert.DoesNotContain(files, f => f.EndsWith(".tmp", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    // Sección 10 de la tarea: un archivo corrupto nunca impide que la app arranque - LoadAsync
    // devuelve null (igual que "no existe archivo"), nunca una excepción ni un valor inventado.
    [Fact]
    public async Task CorruptSettingsFileReturnsNullInsteadOfThrowingOrInventingValues()
    {
        var root = Path.Combine(Path.GetTempPath(), "PosPlatformLocalSettingsStoreTests_" + Guid.NewGuid());
        var pathProvider = new ApplicationPathProvider(root);
        var store = new FileLocalSettingsStore(pathProvider, NullLogger<FileLocalSettingsStore>.Instance);

        try
        {
            var configDirectory = Path.Combine(pathProvider.DataDirectory, "Config");
            Directory.CreateDirectory(configDirectory);
            await File.WriteAllTextAsync(Path.Combine(configDirectory, "settings.json"), "{ not valid json");

            var loaded = await store.LoadAsync();

            Assert.Null(loaded);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SettingsFileLivesUnderTheConfigSubfolderOfDataDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "PosPlatformLocalSettingsStoreTests_" + Guid.NewGuid());
        var pathProvider = new ApplicationPathProvider(root);
        var store = new FileLocalSettingsStore(pathProvider, NullLogger<FileLocalSettingsStore>.Instance);

        try
        {
            await store.SaveAsync(new LocalSettings());

            var expectedPath = Path.Combine(pathProvider.DataDirectory, "Config", "settings.json");
            Assert.True(File.Exists(expectedPath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static FileLocalSettingsStore CreateStore(out Action cleanup)
    {
        var root = Path.Combine(Path.GetTempPath(), "PosPlatformLocalSettingsStoreTests_" + Guid.NewGuid());
        var pathProvider = new ApplicationPathProvider(root);
        var store = new FileLocalSettingsStore(pathProvider, NullLogger<FileLocalSettingsStore>.Instance);

        cleanup = () =>
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        };

        return store;
    }
}
