using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pos.Infrastructure.Logging;
using Pos.Infrastructure.Storage;

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
                    configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false))
                .ConfigureLogging(ConfigureLogging);

        // BASIC-REL-01, sección 12/17/37: reemplaza los proveedores por defecto de
        // Host.CreateDefaultBuilder() (Console/Debug/EventSource/EventLog en Windows — irrelevantes
        // o riesgosos para un WinExe empaquetado: EventLog requiere una fuente de evento registrada
        // en la máquina cliente) por Debug (útil solo en depuración local, sin costo en producción) y
        // el proveedor de archivo rotativo persistente. Se configura como parte de CreateBaseBuilder
        // (no en App.xaml.cs) para que el registro esté disponible desde el primer log posible del
        // Host, tan pronto como sea razonablemente posible dentro del arranque (sección 37).
        //
        // Usa un IApplicationPathProvider construido directamente (no resuelto por DI, que todavía no
        // existe en este punto de ConfigureLogging) — igual patrón que el resto de valores leídos
        // antes de Build() en App.xaml.cs. El directorio de registros se crea de forma perezosa en la
        // primera escritura real (ver RotatingFileLoggerProvider), nunca aquí.
        private static void ConfigureLogging(HostBuilderContext context, ILoggingBuilder logging)
        {
            var pathProvider = new ApplicationPathProvider();

            logging.ClearProviders();
            logging.AddDebug();
            logging.AddProvider(new RotatingFileLoggerProvider(pathProvider.LogsDirectory));
            logging.SetMinimumLevel(LogLevel.Information);
        }
    }
}
