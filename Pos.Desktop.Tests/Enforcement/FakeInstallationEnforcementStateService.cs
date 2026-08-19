using Pos.Application.Enforcement;
using Pos.Application.InstallationHealth;

namespace Pos.Desktop.Tests.Enforcement;

internal sealed class FakeInstallationEnforcementStateService : IInstallationEnforcementStateService
{
    public FakeInstallationEnforcementStateService(InstallationEnforcementState current = InstallationEnforcementState.Allowed)
    {
        Current = current;
    }

    public InstallationEnforcementState Current { get; private set; }

    public event EventHandler<InstallationEnforcementState>? StateChanged;

    public int ApplyHeartbeatOutcomeCallCount { get; private set; }

    public InstallationHeartbeatSendOutcome? LastAppliedOutcome { get; private set; }

    public int ClearCallCount { get; private set; }

    public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task ApplyHeartbeatOutcomeAsync(InstallationHeartbeatSendOutcome outcome, CancellationToken cancellationToken)
    {
        ApplyHeartbeatOutcomeCallCount++;
        LastAppliedOutcome = outcome;

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

    public void RaiseStateChanged(InstallationEnforcementState state)
    {
        Current = state;
        StateChanged?.Invoke(this, state);
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
