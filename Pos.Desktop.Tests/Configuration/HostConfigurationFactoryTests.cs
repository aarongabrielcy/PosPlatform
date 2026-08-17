using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pos.Desktop.Configuration;

namespace Pos.Desktop.Tests.Configuration;

public class HostConfigurationFactoryTests
{
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
