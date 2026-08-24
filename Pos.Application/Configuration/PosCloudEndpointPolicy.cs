namespace Pos.Application.Configuration;

public enum PosCloudEndpointValidationStatus
{
    Valid,
    Missing,
    InvalidUrl,
    RequiresHttps,
    LocalhostNotAllowed,
}

public readonly record struct PosCloudEndpointValidationResult(PosCloudEndpointValidationStatus Status)
{
    public bool IsValid => Status == PosCloudEndpointValidationStatus.Valid;
}

// BASIC-REL-01, sección 26-33: frontera de configuración de release para el endpoint de POS Cloud.
// La URL de producción exacta (REL-CLOUD-URL-01) no existe todavía en este repositorio — esta
// política no la inventa. En cambio, garantiza que un build de Release nunca pueda empaquetarse
// silenciosamente apuntando a localhost o a HTTP plano: Desktop/App.xaml.cs invoca Validate con
// isReleaseBuild = ReleaseBuildInfo.IsReleaseBuild (Debug/Release real del ejecutable publicado, no
// una variable de entorno que pudiera olvidarse de configurar en la máquina del cliente) y falla el
// arranque si el resultado no es válido. En Debug (desarrollo), localhost permanece permitido sin
// cambios (sección 29: "no romper el flujo de trabajo del desarrollador local").
//
// Lógica pura, sin E/S: vive en Pos.Application (solo depende de Pos.Domain, ver CLAUDE.md sección
// B) para poder probarse sin construir ningún host ni ventana WPF, igual criterio que
// Pos.Desktop.StartupFlowCoordinator.
public static class PosCloudEndpointPolicy
{
    public static PosCloudEndpointValidationResult Validate(string? baseUrl, bool isReleaseBuild)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return new PosCloudEndpointValidationResult(PosCloudEndpointValidationStatus.Missing);
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            return new PosCloudEndpointValidationResult(PosCloudEndpointValidationStatus.InvalidUrl);
        }

        if (!isReleaseBuild)
        {
            return new PosCloudEndpointValidationResult(PosCloudEndpointValidationStatus.Valid);
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return new PosCloudEndpointValidationResult(PosCloudEndpointValidationStatus.RequiresHttps);
        }

        // Uri.IsLoopback ya clasifica correctamente "localhost", todo el rango 127.0.0.0/8 y las
        // formas equivalentes de ::1 (con o sin corchetes) — más robusto que comparar uri.Host contra
        // una lista fija de literales.
        if (uri.IsLoopback)
        {
            return new PosCloudEndpointValidationResult(PosCloudEndpointValidationStatus.LocalhostNotAllowed);
        }

        return new PosCloudEndpointValidationResult(PosCloudEndpointValidationStatus.Valid);
    }
}
