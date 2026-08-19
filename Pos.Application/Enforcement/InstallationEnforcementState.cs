namespace Pos.Application.Enforcement;

// Estado de aplicación (enforcement) de licenciamiento a nivel Installation, confirmado por el
// backend vía heartbeat o recuperado/reemplazado vía enrollment (ver sección 12 de la tarea).
// Deliberadamente no incluye Offline/Stale/NetworkFailure: esos son resultados de conectividad
// (ver Pos.Application.InstallationHealth.InstallationHeartbeatSendOutcome), no estados de
// licencia, y nunca deben mutar este enum (sección 25).
public enum InstallationEnforcementState
{
    Allowed,
    Suspended,
    CredentialInvalid,
    Decommissioned,
}
