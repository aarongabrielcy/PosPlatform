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
}
