using System.Reflection;
using Pos.Application.Common.Versioning;

namespace Pos.Infrastructure.InstallationHealth;

// Lee la versión real de la aplicación en ejecución desde los metadatos del ensamblado de entrada.
// La versión comercial (1.0.0 para Basic V1) se define en un único lugar, Directory.Build.props
// (<Version>, ver BASIC-REL-01 sección 23/24) — este proveedor solo la refleja tal cual, sin
// hardcodearla, de modo que un futuro cambio de versión se propague automáticamente al heartbeat y
// a Configuración → Sistema sin tocar este código (ver sección 7 de la tarea de activación).
public sealed class AssemblyApplicationVersionProvider : IApplicationVersionProvider
{
    private const string FallbackVersion = "0.0.0";

    public string GetVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            // Recorta el sufijo de metadatos de build (p. ej. "+<git sha>") que el SDK agrega
            // automáticamente al InformationalVersion cuando el build es determinista: el backend
            // solo necesita la parte semántica (ver record-heartbeat.request.dto.ts, límite de 50
            // caracteres, y appVersion sirve para visibilidad de despliegue, no para trazabilidad
            // de commit).
            var plusIndex = informationalVersion.IndexOf('+');
            return plusIndex >= 0 ? informationalVersion[..plusIndex] : informationalVersion;
        }

        var version = assembly.GetName().Version;
        return version is null ? FallbackVersion : version.ToString();
    }
}
