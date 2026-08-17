using System.Reflection;
using Pos.Application.Common.Versioning;

namespace Pos.Infrastructure.InstallationHealth;

// Lee la versión real de la aplicación en ejecución desde los metadatos del ensamblado de entrada.
// Pos.Desktop.csproj no fija <Version>/<AssemblyVersion>/<InformationalVersion> explícitamente, por
// lo que el SDK de .NET usa el valor por defecto (1.0.0.0) — este proveedor lo refleja tal cual, sin
// hardcodearlo, de modo que un futuro <Version> en el csproj se propague automáticamente al
// heartbeat sin tocar este código (ver sección 7 de la tarea).
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
