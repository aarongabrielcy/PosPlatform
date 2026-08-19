using Pos.Application.Enforcement;
using Pos.Application.InstallationHealth;

namespace Pos.Application.Tests.Enforcement;

internal sealed class FakeInstallationEnforcementStateService : IInstallationEnforcementStateService
{
    public FakeInstallationEnforcementStateService(InstallationEnforcementState current = InstallationEnforcementState.Allowed)
    {
        Current = current;
    }

    public InstallationEnforcementState Current { get; private set; }

    public event EventHandler<InstallationEnforcementState>? StateChanged;

    public int ClearCallCount { get; private set; }

    // Permite a las pruebas de guarda de mutación (secciones 32/33 de la tarea) fijar directamente
    // el estado observado por el servicio bajo prueba, sin pasar por la tabla de transición de
    // ApplyHeartbeatOutcomeAsync.
    public void SetCurrentForTest(InstallationEnforcementState state) => Current = state;

    public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task ApplyHeartbeatOutcomeAsync(InstallationHeartbeatSendOutcome outcome, CancellationToken cancellationToken)
    {
        SetCurrent(outcome switch
        {
            InstallationHeartbeatSendOutcome.Success when Current == InstallationEnforcementState.Suspended =>
                InstallationEnforcementState.Allowed,
            InstallationHeartbeatSendOutcome.Suspended => InstallationEnforcementState.Suspended,
            InstallationHeartbeatSendOutcome.CredentialInvalid => InstallationEnforcementState.CredentialInvalid,
            InstallationHeartbeatSendOutcome.Decommissioned => InstallationEnforcementState.Decommissioned,
            _ => Current,
        });

        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken)
    {
        ClearCallCount++;
        SetCurrent(InstallationEnforcementState.Allowed);
        return Task.CompletedTask;
    }

    private void SetCurrent(InstallationEnforcementState state)
    {
        if (state == Current)
        {
            return;
        }

        Current = state;
        StateChanged?.Invoke(this, state);
    }
}
