using Pos.Application.Enforcement;
using Pos.Application.InstallationHealth;

namespace Pos.Desktop.Tests.Enforcement;

internal sealed class FakeInstallationConnectivityStateService : IInstallationConnectivityStateService
{
    public FakeInstallationConnectivityStateService(
        InstallationConnectivityState current = InstallationConnectivityState.Checking)
    {
        Current = current;
    }

    public InstallationConnectivityState Current { get; private set; }

    public event EventHandler<InstallationConnectivityState>? StateChanged;

    public int ApplyHeartbeatOutcomeCallCount { get; private set; }

    public InstallationHeartbeatSendOutcome? LastAppliedOutcome { get; private set; }

    public void ApplyHeartbeatOutcome(InstallationHeartbeatSendOutcome outcome)
    {
        ApplyHeartbeatOutcomeCallCount++;
        LastAppliedOutcome = outcome;

        var next = outcome switch
        {
            InstallationHeartbeatSendOutcome.Success => InstallationConnectivityState.Connected,
            InstallationHeartbeatSendOutcome.Suspended => InstallationConnectivityState.Connected,
            InstallationHeartbeatSendOutcome.CredentialInvalid => InstallationConnectivityState.Connected,
            InstallationHeartbeatSendOutcome.Decommissioned => InstallationConnectivityState.Connected,
            InstallationHeartbeatSendOutcome.NetworkFailure => InstallationConnectivityState.Offline,
            _ => Current,
        };

        if (next == Current)
        {
            return;
        }

        Current = next;
        StateChanged?.Invoke(this, next);
    }

    public void RaiseStateChanged(InstallationConnectivityState state)
    {
        Current = state;
        StateChanged?.Invoke(this, state);
    }
}
