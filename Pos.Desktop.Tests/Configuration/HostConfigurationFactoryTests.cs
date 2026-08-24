using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pos.Desktop.Configuration;

namespace Pos.Desktop.Tests.Configuration;

public class HostConfigurationFactoryTests
{
    // BASIC-REL-01, sección 12/37/48: solo resuelve ILoggerFactory/ILogger<T> y verifica que el
    // proveedor de registro rotativo quedó registrado, sin invocar ningún método Log*. Un log real
    // escribiría en el LocalAppData real de la máquina que ejecuta la prueba (RotatingFileLoggerProvider
    // resuelve el directorio desde ApplicationPathProvider(), no desde contentRootDirectory) — la
    // sección 48 exige explícitamente no hacer eso desde una prueba automatizada.
    [Fact]
    public void CreateBaseBuilderConfiguresARotatingFileLoggerProviderWithoutWritingAnything()
    {
        var contentRoot = CreateContentRoot();

        try
        {
            File.WriteAllText(
                Path.Combine(contentRoot, "appsettings.json"),
                """{"Activation": {"BaseUrl": "http://localhost:9999"}}""");

            using var host = HostConfigurationFactory.CreateBaseBuilder(contentRoot).Build();

            var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<HostConfigurationFactoryTests>();

            Assert.NotNull(logger);
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    private static string CreateContentRoot()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "PosPlatformHostConfigFactoryTests_" + Guid.NewGuid());
        Directory.CreateDirectory(contentRoot);
        return contentRoot;
    }

    // Prueba de regresión: el content root se resuelve a partir del parámetro explícito recibido
    // por CreateBaseBuilder, nunca de Directory.GetCurrentDirectory(). El directorio temporal usado
    // aquí es, a propósito, distinto del directorio de trabajo actual del proceso de pruebas.
    [Fact]
    public void CreateBaseBuilderResolvesAppsettingsFromExplicitContentRootNotFromProcessCurrentDirectory()
    {
        var contentRoot = CreateContentRoot();

        try
        {
            Assert.NotEqual(Directory.GetCurrentDirectory(), contentRoot);

            File.WriteAllText(
                Path.Combine(contentRoot, "appsettings.json"),
                """{"Activation": {"BaseUrl": "http://localhost:9999"}}""");

            using var host = HostConfigurationFactory.CreateBaseBuilder(contentRoot).Build();
            var configuration = host.Services.GetRequiredService<IConfiguration>();

            Assert.Equal("http://localhost:9999", configuration["Activation:BaseUrl"]);
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    [Fact]
    public void CreateBaseBuilderOverridesWithOptionalLocalSettingsWhenPresent()
    {
        var contentRoot = CreateContentRoot();

        try
        {
            File.WriteAllText(
                Path.Combine(contentRoot, "appsettings.json"),
                """{"Activation": {"BaseUrl": "http://localhost:9999"}}""");
            File.WriteAllText(
                Path.Combine(contentRoot, "appsettings.Local.json"),
                """{"Activation": {"BaseUrl": "http://localhost:8888"}}""");

            using var host = HostConfigurationFactory.CreateBaseBuilder(contentRoot).Build();
            var configuration = host.Services.GetRequiredService<IConfiguration>();

            Assert.Equal("http://localhost:8888", configuration["Activation:BaseUrl"]);
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    [Fact]
    public void CreateBaseBuilderWithoutLocalSettingsKeepsBaseAppsettingsValue()
    {
        var contentRoot = CreateContentRoot();

        try
        {
            File.WriteAllText(
                Path.Combine(contentRoot, "appsettings.json"),
                """{"Activation": {"BaseUrl": "http://localhost:9999"}}""");

            using var host = HostConfigurationFactory.CreateBaseBuilder(contentRoot).Build();
            var configuration = host.Services.GetRequiredService<IConfiguration>();

            Assert.Equal("http://localhost:9999", configuration["Activation:BaseUrl"]);
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    // BASIC-INS-01, sección 12/13: prueba de regresión de la estrategia de inyección del endpoint de
    // producción. eng\build-release.ps1 genera appsettings.Production.json (nunca el
    // appsettings.json versionado) dentro del directorio de publish, y Host.CreateDefaultBuilder()
    // ya carga "appsettings.{EnvironmentName}.json" con mayor precedencia que "appsettings.json" de
    // forma nativa — sin ningún cambio de código adicional aquí — cuando DOTNET_ENVIRONMENT no está
    // configurado (o vale "Production"), que es el caso real de una máquina cliente sin ese valor de
    // entorno. appsettings.Local.json (agregado explícitamente por CreateBaseBuilder, ver arriba)
    // conserva la precedencia más alta para desarrollo local, sin verse afectado por este cambio.
    [Fact]
    public void CreateBaseBuilderLoadsProductionAppsettingsOverBaseAppsettings()
    {
        var contentRoot = CreateContentRoot();
        var originalEnvironment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

        try
        {
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Production");

            File.WriteAllText(
                Path.Combine(contentRoot, "appsettings.json"),
                """{"Activation": {"BaseUrl": "http://localhost:9999"}}""");
            File.WriteAllText(
                Path.Combine(contentRoot, "appsettings.Production.json"),
                """{"Activation": {"BaseUrl": "https://cloud.posplatform-release.example.com"}}""");

            using var host = HostConfigurationFactory.CreateBaseBuilder(contentRoot).Build();
            var configuration = host.Services.GetRequiredService<IConfiguration>();

            Assert.Equal("https://cloud.posplatform-release.example.com", configuration["Activation:BaseUrl"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", originalEnvironment);
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    // BASIC-INS-01, sección 14-16 (REL-LOG-02): prueba directamente ConfigureLogging (sin construir
    // el Host completo, que necesitaría un content root) verificando IsEnabled — nunca escribe una
    // línea de log real, así que no toca el LocalAppData real de la máquina que ejecuta la prueba
    // (RotatingFileLoggerProvider crea su directorio de forma perezosa solo en la primera escritura,
    // ver ese proveedor). isReleaseBuild se pasa explícitamente como parámetro precisamente para
    // poder probar ambas ramas en una sola ejecución, sin importar si Pos.Desktop.Tests se compiló en
    // Debug o Release.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConfigureLoggingFiltersEfCoreDatabaseCommandOnlyInRelease(bool isReleaseBuild)
    {
        var services = new ServiceCollection();
        services.AddLogging(logging => HostConfigurationFactory.ConfigureLogging(logging, isReleaseBuild));

        using var provider = services.BuildServiceProvider();
        var loggerFactory = provider.GetRequiredService<ILoggerFactory>();

        var efCommandLogger = loggerFactory.CreateLogger("Microsoft.EntityFrameworkCore.Database.Command");
        var posPlatformLogger = loggerFactory.CreateLogger("Pos.Desktop.App");

        // En Release, el Information rutinario de EF Core queda filtrado; en Debug sigue habilitado.
        Assert.Equal(!isReleaseBuild, efCommandLogger.IsEnabled(LogLevel.Information));

        // Warning/Error de EF Core NUNCA se filtran, en ninguna configuración.
        Assert.True(efCommandLogger.IsEnabled(LogLevel.Warning));
        Assert.True(efCommandLogger.IsEnabled(LogLevel.Error));

        // Los eventos Information propios de PosPlatform (arranque, versión, heartbeat, enforcement,
        // conectividad) nunca se ven afectados por este filtro específico de EF Core.
        Assert.True(posPlatformLogger.IsEnabled(LogLevel.Information));
        Assert.True(posPlatformLogger.IsEnabled(LogLevel.Warning));
    }
}
