namespace Pos.Application.Enforcement;

// Estado de conectividad CONTROL-PLANE (BASIC-UX-01, sección 27/29): responde únicamente "¿puede
// este Desktop comunicarse ahora mismo con POSPlatform Cloud/control plane?" (heartbeat de
// instalación/activación). Deliberadamente NO representa Business Sync (ventas/inventario/backup
// sincronizados): esa funcionalidad no existe todavía. Nunca debe usarse como guarda de
// autorización — ver IInstallationConnectivityStateService.
public enum InstallationConnectivityState
{
    // Estado inicial, antes de que se conozca el resultado del primer heartbeat de esta sesión
    // (sección 36): nunca se infiere "Connected" solo porque la interfaz de red esté arriba, haya
    // datos de activación previos o la app se haya ejecutado antes.
    Checking,

    // El servidor respondió (éxito o un rechazo válido como Suspended/CredentialInvalid/
    // Decommissioned): eso YA prueba que el servidor fue alcanzable, aunque la instalación esté
    // restringida (sección 31/32).
    Connected,

    // Fallo de red al intentar el heartbeat: no dice nada sobre el estado de enforcement
    // persistido, que permanece exactamente como estaba (sección 33).
    Offline,
}
