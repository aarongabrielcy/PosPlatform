using Pos.Application.Configuration;
using Pos.Application.Receipts;

namespace Pos.Desktop.Configuration;

// BASIC-CFG-01, sección 35: implementa la precedencia de configuración documentada en el reporte
// final -
//
//   appsettings.json (default de fábrica)
//         -> appsettings.Local.json (override de desarrollo/operador, ya fusionado por
//            HostConfigurationFactory dentro de IConfiguration antes de llegar aquí)
//         -> LocalAppData (Configuración > Impresora, ILocalSettingsStore)
//
// "_configuredDefaults" es exactamente lo que ReceiptPrinterOptionsFactory.Create ya leía en
// BASIC-PRN-01 (primeras dos capas, colapsadas en una sola porque IConfiguration ya las fusionó).
// Si ILocalSettingsStore no tiene nada guardado (primer arranque) o el archivo está corrupto,
// Current cae de vuelta a _configuredDefaults sin alterar el comportamiento de una instalación
// existente (sección 34/36/37 de la tarea). RefreshAsync se llama una vez al arrancar y de nuevo
// tras cada Guardar exitoso en Configuración > Impresora (sección 32/33: sin reiniciar la app).
internal sealed class ReceiptPrinterOptionsProvider : IReceiptPrinterOptionsProvider
{
    private readonly ReceiptPrinterOptions _configuredDefaults;
    private readonly ILocalSettingsStore _localSettingsStore;
    private volatile ReceiptPrinterOptions _current;

    public ReceiptPrinterOptionsProvider(ReceiptPrinterOptions configuredDefaults, ILocalSettingsStore localSettingsStore)
    {
        _configuredDefaults = configuredDefaults ?? throw new ArgumentNullException(nameof(configuredDefaults));
        _localSettingsStore = localSettingsStore ?? throw new ArgumentNullException(nameof(localSettingsStore));
        _current = configuredDefaults;
    }

    public ReceiptPrinterOptions Current => _current;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _localSettingsStore.LoadAsync(cancellationToken).ConfigureAwait(false);

        _current = settings?.ReceiptPrinter ?? _configuredDefaults;
    }
}
