namespace Pos.Application.InstallationHealth;

public enum InstallationHeartbeatSendOutcome
{
    // Sin registro de activación local: no se intenta ningún heartbeat (ver sección 5 de la tarea).
    NotActivated,

    // Hay registro de activación, pero la credencial local no está disponible: anomalía operativa,
    // no un fallo de red. No se intenta ningún heartbeat (ver sección 11 de la tarea).
    CredentialMissing,

    Success,
    CredentialInvalid,
    Suspended,
    Decommissioned,
    NetworkFailure,
}
