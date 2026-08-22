namespace Pos.Application.Receipts;

// Configuración de impresión de tickets (sección 18/20 de la tarea). POCO simple sin dependencia de
// Microsoft.Extensions.Options (Pos.Application no tiene PackageReference alguno, ver
// Pos.Architecture.Tests.ProductionProjectsShouldNotContainPackageReferences): Pos.Desktop lo puebla
// leyendo IConfiguration directamente (appsettings.json / appsettings.Local.json, clave
// "ReceiptPrinter") y lo registra como instancia singleton. BASIC-CFG-01 podrá sustituir esta
// lectura por una pantalla real sin tocar esta forma (sección 20: "avoid designing options that
// require refactoring later").
public sealed class ReceiptPrinterOptions
{
    // Apagado por defecto (sección 19): un desarrollo o una tienda sin impresora configurada debe
    // arrancar y vender con normalidad. Producción exige configurarlo explícitamente.
    public bool Enabled { get; init; }

    // Nombre exacto de la impresora tal como aparece en Windows (Panel de control > Dispositivos e
    // impresoras). Nunca hardcoded (sección 18): siempre proviene de configuración.
    public string? PrinterName { get; init; }

    public ReceiptPaperWidth PaperWidth { get; init; } = ReceiptPaperWidth.Mm80;

    // Solo gobierna la impresión automática tras un cobro exitoso (sección 21). El reimpreso manual
    // desde Historial ignora este valor: es una acción explícita del usuario.
    public bool AutoPrint { get; init; } = true;

    public bool CutPaper { get; init; }
}
