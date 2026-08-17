using Pos.Infrastructure.InstallationHealth;

namespace Pos.Infrastructure.Tests.InstallationHealth;

public class AssemblyApplicationVersionProviderTests
{
    // Deliberadamente no se afirma un valor literal (p. ej. "1.0.0"): el csproj no fija <Version>
    // hoy, así que el SDK genera el valor por defecto, y ese valor por defecto puede cambiar sin
    // que este proveedor deba tocarse (ver sección 28 de la tarea: evitar pruebas frágiles atadas a
    // la versión literal de hoy).
    [Fact]
    public void GetVersionReturnsANonEmptyValue()
    {
        var provider = new AssemblyApplicationVersionProvider();

        var version = provider.GetVersion();

        Assert.False(string.IsNullOrWhiteSpace(version));
    }

    [Fact]
    public void GetVersionNeverIncludesBuildMetadataSuffix()
    {
        var provider = new AssemblyApplicationVersionProvider();

        var version = provider.GetVersion();

        Assert.DoesNotContain('+', version);
    }

    [Fact]
    public void GetVersionFitsWithinTheBackendAppVersionLengthLimit()
    {
        var provider = new AssemblyApplicationVersionProvider();

        var version = provider.GetVersion();

        // record-heartbeat.request.dto.ts: @Length(1, 50).
        Assert.InRange(version.Length, 1, 50);
    }
}
