using Pos.Application.InstallationHealth;

namespace Pos.Application.Enforcement;

// Fuente única de verdad en memoria (respaldada por IInstallationEnforcementStateStore) para el
// estado de enforcement de esta Installation. Es Singleton (ver DependencyInjection) porque debe
// observarse de forma idéntica desde InstallationHeartbeatBackgroundService (ámbito raíz del Host)
// y desde los servicios de Application con ámbito Scoped que protegen mutaciones (sección 17/19 de
// la tarea): un servicio Scoped por request/uso no serviría para notificar a una UI ya abierta.
public interface IInstallationEnforcementStateService
{
    InstallationEnforcementState Current { get; }

    // Notifica cambios de estado sin que InstallationHeartbeatBackgroundService (un BackgroundService
    // sin acceso a WPF) manipule controles directamente (sección 17): Desktop se suscribe en el
    // límite de composición/UI (App.xaml.cs o MainWindowViewModel).
    event EventHandler<InstallationEnforcementState>? StateChanged;

    // Carga el estado persistido al arranque, antes de que StartupFlowCoordinator decida el flujo.
    // Debe invocarse exactamente una vez, antes de leer Current por primera vez.
    Task InitializeAsync(CancellationToken cancellationToken);

    // Aplica la tabla de transición de la sección 16 de la tarea a partir de un resultado de
    // heartbeat confirmado. NetworkFailure/NotActivated/CredentialMissing nunca cambian el estado.
    Task ApplyHeartbeatOutcomeAsync(InstallationHeartbeatSendOutcome outcome, CancellationToken cancellationToken);

    // Usado exclusivamente por el flujo de recuperación de credencial (sección 8) y por el flujo de
    // activación como nueva Installation (sección 10), después de que la persistencia local de la
    // nueva credencial (y, en su caso, del nuevo registro de activación) haya tenido éxito.
    Task ClearAsync(CancellationToken cancellationToken);
}
