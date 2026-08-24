namespace Pos.Application.Configuration;

public enum ReleasePackagingEndpointValidationStatus
{
    Valid,
    Missing,
    InvalidUrl,
    RequiresHttps,
    LocalhostNotAllowed,
    ReservedPlaceholderNotAllowed,
}

public readonly record struct ReleasePackagingEndpointValidationResult(ReleasePackagingEndpointValidationStatus Status)
{
    public bool IsValid => Status == ReleasePackagingEndpointValidationStatus.Valid;
}

// BASIC-INS-01, sección 9-11: frontera de validación adicional para el EMPAQUETADO del instalador,
// distinta de PosCloudEndpointPolicy (que protege el binario ya instalado, en tiempo de arranque).
// Reutiliza esa misma política como base (HTTPS/no-localhost, isReleaseBuild siempre true: un build
// de prueba también debe producir un binario que arrancaría igual que uno de producción) y agrega el
// rechazo de dominios de marcador de posición reservados (.invalid/.test/.example) para builds
// FINALES, permitiéndolos únicamente cuando isTestBuild=true (sección 11: instalador de prueba para
// validación técnica, nunca confundible con distribución comercial).
//
// eng\build-release.ps1 replica estas mismas reglas en su función Test-ReleasePackagingEndpoint (ver
// comentario allí) en vez de invocar este ensamblado por reflexión: cargar Pos.Application.dll junto
// con su grafo de dependencias transitivas (Pos.Domain y paquetes NuGet) desde un script de
// PowerShell es frágil y no aporta valor frente a duplicar ~15 líneas de lógica trivial. Esta clase,
// probada por xunit (Pos.Application.Tests), es la fuente de verdad que ese script debe seguir
// replicando fielmente ante cualquier cambio de regla aquí.
public static class ReleasePackagingEndpointPolicy
{
    private static readonly string[] ReservedPlaceholderSuffixes = { ".invalid", ".test", ".example" };

    public static ReleasePackagingEndpointValidationResult Validate(string? baseUrl, bool isTestBuild)
    {
        var baseResult = PosCloudEndpointPolicy.Validate(baseUrl, isReleaseBuild: true);

        if (!baseResult.IsValid)
        {
            return new ReleasePackagingEndpointValidationResult(MapBaseStatus(baseResult.Status));
        }

        if (!isTestBuild && Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) && IsReservedPlaceholderHost(uri.Host))
        {
            return new ReleasePackagingEndpointValidationResult(ReleasePackagingEndpointValidationStatus.ReservedPlaceholderNotAllowed);
        }

        return new ReleasePackagingEndpointValidationResult(ReleasePackagingEndpointValidationStatus.Valid);
    }

    private static bool IsReservedPlaceholderHost(string host)
    {
        foreach (var suffix in ReservedPlaceholderSuffixes)
        {
            if (host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static ReleasePackagingEndpointValidationStatus MapBaseStatus(PosCloudEndpointValidationStatus status) => status switch
    {
        PosCloudEndpointValidationStatus.Missing => ReleasePackagingEndpointValidationStatus.Missing,
        PosCloudEndpointValidationStatus.InvalidUrl => ReleasePackagingEndpointValidationStatus.InvalidUrl,
        PosCloudEndpointValidationStatus.RequiresHttps => ReleasePackagingEndpointValidationStatus.RequiresHttps,
        PosCloudEndpointValidationStatus.LocalhostNotAllowed => ReleasePackagingEndpointValidationStatus.LocalhostNotAllowed,
        _ => ReleasePackagingEndpointValidationStatus.InvalidUrl,
    };
}
