namespace Pos.Application.Configuration;

// BASIC-CFG-01, sección 8/10: puerto de persistencia de la configuración local de máquina.
// LoadAsync devuelve null tanto cuando no existe archivo (primer arranque) como cuando el archivo
// existe pero está corrupto/ilegible (sección 10: "corrupt local config -> safe defaults ->
// application still starts") - en ambos casos el llamador debe recurrir a los valores por defecto
// vigentes (appsettings/appsettings.Local.json), nunca inventar un valor distinto. La implementación
// (Pos.Infrastructure) es responsable de registrar el diagnóstico correspondiente en cada caso.
public interface ILocalSettingsStore
{
    Task<LocalSettings?> LoadAsync(CancellationToken cancellationToken = default);

    Task<bool> SaveAsync(LocalSettings settings, CancellationToken cancellationToken = default);
}
