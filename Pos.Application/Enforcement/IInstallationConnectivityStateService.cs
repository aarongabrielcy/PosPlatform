using Pos.Application.InstallationHealth;

namespace Pos.Application.Enforcement;

// Fuente única de verdad EN MEMORIA (sin persistencia: sección 36 exige que cada sesión empiece en
// Checking, nunca reponer un estado "Connected" de una ejecución anterior) para el indicador de
// conectividad POS Cloud del header (BASIC-UX-01, sección 27-38). Singleton, mismo motivo que
// IInstallationEnforcementStateService: debe observarse igual desde
// InstallationHeartbeatBackgroundService (que también alimenta este estado, sección 30 — nunca un
// segundo poll HTTP) y desde la UI ya abierta.
//
// Deliberadamente separado de IInstallationEnforcementStateService (sección 29/32): un heartbeat
// puede probar que el servidor es alcanzable (Connected) mientras la instalación sigue restringida
// (Suspended/CredentialInvalid/Decommissioned). Este servicio nunca debe usarse como guarda de
// autorización — esa responsabilidad es exclusiva de IInstallationEnforcementStateService.
public interface IInstallationConnectivityStateService
{
    InstallationConnectivityState Current { get; }

    event EventHandler<InstallationConnectivityState>? StateChanged;

    // Traduce un resultado de heartbeat confirmado a Connected/Offline (sección 31). NotActivated y
    // CredentialMissing nunca intentaron un heartbeat real: no son evidencia de conectividad ni de
    // su ausencia, así que no cambian el estado actual.
    void ApplyHeartbeatOutcome(InstallationHeartbeatSendOutcome outcome);
}
