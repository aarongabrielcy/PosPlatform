using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Pos.Desktop.Configuration
{
    // Host.CreateDefaultBuilder() usa Directory.GetCurrentDirectory() como content root por
    // defecto, tanto para su carga implícita de appsettings.json como para AddJsonFile con rutas
    // relativas. Eso hace que la configuración dependa del directorio de trabajo del proceso
    // (distinto entre "dotnet run --project Pos.Desktop\..." desde la raíz de la solución y
    // "dotnet run" desde Pos.Desktop, y distinto también del directorio del ejecutable publicado).
    // Este método fija explícitamente el content root al recibido por parámetro —en producción,
    // AppContext.BaseDirectory— para que appsettings.json y appsettings.Local.json (opcional, no
    // versionado, ver .gitignore) se resuelvan siempre junto al binario, sin importar cómo se
    // invoque el proceso. Aislado en su propio método para poder probarlo sin construir el árbol
    // completo de ventanas WPF.
    internal static class HostConfigurationFactory
    {
        public static IHostBuilder CreateBaseBuilder(string contentRootDirectory) =>
            Host.CreateDefaultBuilder()
                .UseContentRoot(contentRootDirectory)
                .ConfigureAppConfiguration(configuration =>
                    configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false));
    }
}
