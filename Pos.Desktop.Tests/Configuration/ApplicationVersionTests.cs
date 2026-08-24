using System.Reflection;

namespace Pos.Desktop.Tests.Configuration;

// BASIC-REL-01, sección 23/25/49: prueba deliberadamente el ensamblado real de Pos.Desktop
// (typeof(App).Assembly), no Assembly.GetEntryAssembly() — en un host de pruebas ese método
// devolvería el ensamblado de pruebas, no el binario comercial que realmente se publica (ver
// AssemblyApplicationVersionProvider, que sí usa GetEntryAssembly correctamente en producción).
public class ApplicationVersionTests
{
    [Fact]
    public void PosDesktopAssemblyReportsCommercialVersionOnePointZeroPointZero()
    {
        var assembly = typeof(App).Assembly;
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        Assert.False(string.IsNullOrWhiteSpace(informationalVersion));

        var plusIndex = informationalVersion!.IndexOf('+');
        var version = plusIndex >= 0 ? informationalVersion[..plusIndex] : informationalVersion;

        Assert.Equal("1.0.0", version);
    }

    [Fact]
    public void PosDesktopAssemblyFileVersionIsOnePointZeroPointZeroPointZero()
    {
        var assembly = typeof(App).Assembly;
        var fileVersion = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;

        Assert.Equal("1.0.0.0", fileVersion);
    }
}
