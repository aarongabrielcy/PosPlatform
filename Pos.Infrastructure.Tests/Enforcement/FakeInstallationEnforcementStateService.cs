using Pos.Application.Enforcement;
using Pos.Application.InstallationHealth;

namespace Pos.Infrastructure.Tests.Enforcement;

// Duplicado deliberado del fake homónimo en Pos.Application.Tests: Pos.Infrastructure.Tests no
// referencia ese proyecto (ver Pos.Infrastructure.Tests.csproj), igual que el resto de los fakes de
// prueba de este repositorio, que nunca se comparten entre proyectos de test.
internal sealed class FakeInstallationEnforcementStateService : IInstallationEnforcementStateService
{
    public FakeInstallationEnforcementStateService(InstallationEnforcementState current = InstallationEnforcementState.Allowed)
    {
        Current = current;
    }

    public InstallationEnforcementState Current { get; private set; }

    public event EventHandler<InstallationEnforcementState>? StateChanged;

    public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task ApplyHeartbeatOutcomeAsync(InstallationHeartbeatSendOutcome outcome, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task ClearAsync(CancellationToken cancellationToken)
    {
        if (Current != InstallationEnforcementState.Allowed)
        {
            Current = InstallationEnforcementState.Allowed;
            StateChanged?.Invoke(this, InstallationEnforcementState.Allowed);
        }

        return Task.CompletedTask;
    }
}
