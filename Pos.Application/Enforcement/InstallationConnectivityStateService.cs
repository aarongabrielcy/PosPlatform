using Pos.Application.InstallationHealth;

namespace Pos.Application.Enforcement;

// Implementación en memoria, sin persistencia (ver IInstallationConnectivityStateService). Un lock
// exclusivo evita una condición de carrera teórica si en algún momento hubiera más de un llamador
// concurrente; hoy el único llamador es InstallationHeartbeatBackgroundService (nunca concurrente
// consigo mismo), igual justificación que InstallationEnforcementStateService.
public sealed class InstallationConnectivityStateService : IInstallationConnectivityStateService
{
    private readonly object _sync = new();

    private InstallationConnectivityState _current = InstallationConnectivityState.Checking;

    public InstallationConnectivityState Current
    {
        get
        {
            lock (_sync)
            {
                return _current;
            }
        }
    }

    public event EventHandler<InstallationConnectivityState>? StateChanged;

    public void ApplyHeartbeatOutcome(InstallationHeartbeatSendOutcome outcome)
    {
        InstallationConnectivityState? next = outcome switch
        {
            InstallationHeartbeatSendOutcome.Success => InstallationConnectivityState.Connected,
            InstallationHeartbeatSendOutcome.Suspended => InstallationConnectivityState.Connected,
            InstallationHeartbeatSendOutcome.CredentialInvalid => InstallationConnectivityState.Connected,
            InstallationHeartbeatSendOutcome.Decommissioned => InstallationConnectivityState.Connected,
            InstallationHeartbeatSendOutcome.NetworkFailure => InstallationConnectivityState.Offline,

            // NotActivated/CredentialMissing nunca intentaron un heartbeat real (sección 31): no
            // cambian el estado actual.
            _ => null,
        };

        if (next is not { } newState)
        {
            return;
        }

        lock (_sync)
        {
            var previous = _current;
            _current = newState;

            if (previous == newState)
            {
                return;
            }
        }

        StateChanged?.Invoke(this, newState);
    }
}
