namespace Pos.Application.Configuration;

// BASIC-CFG-01, sección 27/46: puerto de orquestación para GUARDAR configuración local de máquina.
// A diferencia de ILocalSettingsStore (I/O puro, sin noción de usuario - mismo criterio que
// FileInstallationEnforcementStateStore), este servicio exige Permission.ManageSettings a nivel de
// Application, no solo ocultando el ítem de navegación en Desktop (sección 46: "Application service
// denies mutation"). También es responsable de refrescar IReceiptPrinterOptionsProvider tras un
// guardado exitoso, para que la impresión honre el cambio sin reiniciar la aplicación (sección 32).
public interface ILocalSettingsService
{
    Task<LocalSettingsSaveResult> SaveAsync(LocalSettings settings, CancellationToken cancellationToken = default);
}
