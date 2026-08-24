using System.IO;

namespace Pos.Application.Tests.Configuration;

// BASIC-INS-01 (corrección post-compilación real): regresión mínima para el defecto de sintaxis
// Inno Setup detectado al compilar installer\PosPlatform.iss con el ISCC.exe real ("Unknown
// constant" / "Use two consecutive '{' characters if trying to embed a literal '{'"). El AppId es
// estable y está congelado para futuras actualizaciones in-place — este test solo verifica la
// sintaxis de escape de la llave y que el GUID en sí no haya cambiado, no re-implementa Inno Setup.
public class InstallerScriptSourceTests
{
    private const string StableAppGuid = "B7B6E1F0-9E3E-4E7A-9A9B-3C7C8B7C6B21";

    [Fact]
    public void AppIdDefineEscapesLiteralOpeningBraceAndKeepsStableGuid()
    {
        var content = File.ReadAllText(GetInstallerScriptPath());

        Assert.Contains($"#define MyAppId \"{{{{{StableAppGuid}}}\"", content);
        Assert.DoesNotContain($"#define MyAppId \"{{{StableAppGuid}}}\"", content);
    }

    private static string GetInstallerScriptPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PosPlatform.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new DirectoryNotFoundException("No se encontró la raíz del repositorio (PosPlatform.sln) a partir de AppContext.BaseDirectory.");
        }

        return Path.Combine(directory.FullName, "installer", "PosPlatform.iss");
    }
}
