using Pos.Application.Receipts;

namespace Pos.Application.Configuration;

// BASIC-CFG-01, sección 11: esquema pequeño y deliberadamente cerrado de configuración MÁQUINA
// (no de negocio), persistido en %LOCALAPPDATA%\PosPlatform\Data\Config\settings.json (ver
// ILocalSettingsStore/FileLocalSettingsStore). Reutiliza ReceiptPrinterOptions tal cual (ya definido
// en BASIC-PRN-01) en vez de duplicar un DTO con las mismas cinco propiedades: es exactamente el
// formulario que la pantalla de Configuración > Impresora edita.
public sealed class LocalSettings
{
    public int SchemaVersion { get; init; } = 1;

    public ReceiptPrinterOptions ReceiptPrinter { get; init; } = new();

    public LocalCashDrawerSettings CashDrawer { get; init; } = new();
}
