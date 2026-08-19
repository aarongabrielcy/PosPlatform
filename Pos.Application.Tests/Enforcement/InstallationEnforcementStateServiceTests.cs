using Pos.Application.Enforcement;
using Pos.Application.InstallationHealth;

namespace Pos.Application.Tests.Enforcement;

// Cubre la tabla de transición completa de la sección 16 de la tarea (ver también secciones 29-31:
// TESTS — STATE TRANSITIONS / PERSISTENCE / NETWORK OFFLINE).
public class InstallationEnforcementStateServiceTests
{
    [Fact]
    public async Task AllowedPlusSuccessStaysAllowed()
    {
        var service = await CreateInitializedServiceAsync();

        await service.ApplyHeartbeatOutcomeAsync(InstallationHeartbeatSendOutcome.Success, CancellationToken.None);

        Assert.Equal(InstallationEnforcementState.Allowed, service.Current);
    }

    [Fact]
    public async Task AllowedPlusNetworkFailureStaysAllowed()
    {
        var service = await CreateInitializedServiceAsync();

        await service.ApplyHeartbeatOutcomeAsync(InstallationHeartbeatSendOutcome.NetworkFailure, CancellationToken.None);

        Assert.Equal(InstallationEnforcementState.Allowed, service.Current);
    }

    [Fact]
    public async Task AllowedPlusSuspendedBecomesSuspended()
    {
        var service = await CreateInitializedServiceAsync();

        await service.ApplyHeartbeatOutcomeAsync(InstallationHeartbeatSendOutcome.Suspended, CancellationToken.None);

        Assert.Equal(InstallationEnforcementState.Suspended, service.Current);
    }

    [Fact]
    public async Task SuspendedPlusNetworkFailureStaysSuspended()
    {
        var (service, store) = await CreateInitializedServiceWithStoreAsync();
        await service.ApplyHeartbeatOutcomeAsync(InstallationHeartbeatSendOutcome.Suspended, CancellationToken.None);

        await service.ApplyHeartbeatOutcomeAsync(InstallationHeartbeatSendOutcome.NetworkFailure, CancellationToken.None);

        Assert.Equal(InstallationEnforcementState.Suspended, service.Current);
        Assert.Equal(InstallationEnforcementState.Suspended, store.CurrentlyPersisted);
    }

    [Fact]
    public async Task SuspendedPlusSuccessBecomesAllowed()
    {
        var service = await CreateInitializedServiceAsync();
        await service.ApplyHeartbeatOutcomeAsync(InstallationHeartbeatSendOutcome.Suspended, CancellationToken.None);

        await service.ApplyHeartbeatOutcomeAsync(InstallationHeartbeatSendOutcome.Success, CancellationToken.None);

        Assert.Equal(InstallationEnforcementState.Allowed, service.Current);
    }

    [Fact]
    public async Task AllowedPlusCredentialInvalidBecomesCredentialInvalid()
    {
        var service = await CreateInitializedServiceAsync();

        await service.ApplyHeartbeatOutcomeAsync(InstallationHeartbeatSendOutcome.CredentialInvalid, CancellationToken.None);

        Assert.Equal(InstallationEnforcementState.CredentialInvalid, service.Current);
    }

    [Fact]
    public async Task CredentialInvalidPlusNetworkFailureStaysCredentialInvalid()
    {
        var service = await CreateInitializedServiceAsync();
        await service.ApplyHeartbeatOutcomeAsync(InstallationHeartbeatSendOutcome.CredentialInvalid, CancellationToken.None);

        await service.ApplyHeartbeatOutcomeAsync(InstallationHeartbeatSendOutcome.NetworkFailure, CancellationToken.None);

        Assert.Equal(InstallationEnforcementState.CredentialInvalid, service.Current);
    }

    // CredentialInvalid + Success (sin recuperación explícita) NO se limpia por sí solo: solo
    // Suspended se limpia automáticamente con un heartbeat exitoso (sección 16).
    [Fact]
    public async Task CredentialInvalidPlusSuccessDoesNotBecomeAllowedWithoutExplicitRecovery()
    {
        var service = await CreateInitializedServiceAsync();
        await service.ApplyHeartbeatOutcomeAsync(InstallationHeartbeatSendOutcome.CredentialInvalid, CancellationToken.None);

        await service.ApplyHeartbeatOutcomeAsync(InstallationHeartbeatSendOutcome.Success, CancellationToken.None);

        Assert.Equal(InstallationEnforcementState.CredentialInvalid, service.Current);
    }

    [Fact]
    public async Task AllowedPlusDecommissionedBecomesDecommissioned()
    {
        var service = await CreateInitializedServiceAsync();

        await service.ApplyHeartbeatOutcomeAsync(InstallationHeartbeatSendOutcome.Decommissioned, CancellationToken.None);

        Assert.Equal(InstallationEnforcementState.Decommissioned, service.Current);
    }

    [Fact]
    public async Task DecommissionedPlusNetworkFailureStaysDecommissioned()
    {
        var service = await CreateInitializedServiceAsync();
        await service.ApplyHeartbeatOutcomeAsync(InstallationHeartbeatSendOutcome.Decommissioned, CancellationToken.None);

        await service.ApplyHeartbeatOutcomeAsync(InstallationHeartbeatSendOutcome.NetworkFailure, CancellationToken.None);

        Assert.Equal(InstallationEnforcementState.Decommissioned, service.Current);
    }

    // Decommissioned es terminal salvo reactivación explícita como nueva Installation (ClearAsync,
    // invocado únicamente por InstallationActivationStateService.ActivateAsNewInstallationAsync
    // tras persistencia exitosa — ver InstallationActivationStateServiceTests). Un heartbeat exitoso
    // por sí solo nunca lo revierte (sección 16/29).
    [Fact]
    public async Task DecommissionedPlusSuccessDoesNotBecomeAllowedWithoutNewInstallationActivation()
    {
        var service = await CreateInitializedServiceAsync();
        await service.ApplyHeartbeatOutcomeAsync(InstallationHeartbeatSendOutcome.Decommissioned, CancellationToken.None);

        await service.ApplyHeartbeatOutcomeAsync(InstallationHeartbeatSendOutcome.Success, CancellationToken.None);

        Assert.Equal(InstallationEnforcementState.Decommissioned, service.Current);
    }

    [Theory]
    [InlineData(InstallationHeartbeatSendOutcome.NotActivated)]
    [InlineData(InstallationHeartbeatSendOutcome.CredentialMissing)]
    public async Task PreActivationOutcomesNeverChangeEnforcementState(InstallationHeartbeatSendOutcome outcome)
    {
        var service = await CreateInitializedServiceAsync();

        await service.ApplyHeartbeatOutcomeAsync(outcome, CancellationToken.None);

        Assert.Equal(InstallationEnforcementState.Allowed, service.Current);
    }

    // ---------- Persistencia (sección 13/30) ----------

    [Fact]
    public async Task SuspendedStatePersistsAcrossRestart()
    {
        var (service, store) = await CreateInitializedServiceWithStoreAsync();
        await service.ApplyHeartbeatOutcomeAsync(InstallationHeartbeatSendOutcome.Suspended, CancellationToken.None);

        // "Reinicio del proceso": una nueva instancia respaldada por el mismo store persistido.
        var restarted = new InstallationEnforcementStateService(store);
        await restarted.InitializeAsync(CancellationToken.None);

        Assert.Equal(InstallationEnforcementState.Suspended, restarted.Current);
    }

    [Fact]
    public async Task CredentialInvalidStatePersistsAcrossRestart()
    {
        var (service, store) = await CreateInitializedServiceWithStoreAsync();
        await service.ApplyHeartbeatOutcomeAsync(InstallationHeartbeatSendOutcome.CredentialInvalid, CancellationToken.None);

        var restarted = new InstallationEnforcementStateService(store);
        await restarted.InitializeAsync(CancellationToken.None);

        Assert.Equal(InstallationEnforcementState.CredentialInvalid, restarted.Current);
    }

    [Fact]
    public async Task DecommissionedStatePersistsAcrossRestart()
    {
        var (service, store) = await CreateInitializedServiceWithStoreAsync();
        await service.ApplyHeartbeatOutcomeAsync(InstallationHeartbeatSendOutcome.Decommissioned, CancellationToken.None);

        var restarted = new InstallationEnforcementStateService(store);
        await restarted.InitializeAsync(CancellationToken.None);

        Assert.Equal(InstallationEnforcementState.Decommissioned, restarted.Current);
    }

    [Fact]
    public async Task ClearAsyncRestoresAllowedAndPersistsTheClear()
    {
        var (service, store) = await CreateInitializedServiceWithStoreAsync();
        await service.ApplyHeartbeatOutcomeAsync(InstallationHeartbeatSendOutcome.Suspended, CancellationToken.None);

        await service.ClearAsync(CancellationToken.None);

        Assert.Equal(InstallationEnforcementState.Allowed, service.Current);
        Assert.Equal(1, store.ClearCallCount);
        Assert.Null(store.CurrentlyPersisted);
    }

    // ---------- StateChanged (sección 17) ----------

    [Fact]
    public async Task StateChangedFiresOnlyWhenTheStateActuallyChanges()
    {
        var service = await CreateInitializedServiceAsync();
        var raisedStates = new List<InstallationEnforcementState>();
        service.StateChanged += (_, state) => raisedStates.Add(state);

        await service.ApplyHeartbeatOutcomeAsync(InstallationHeartbeatSendOutcome.Suspended, CancellationToken.None);
        await service.ApplyHeartbeatOutcomeAsync(InstallationHeartbeatSendOutcome.Suspended, CancellationToken.None);
        await service.ApplyHeartbeatOutcomeAsync(InstallationHeartbeatSendOutcome.Success, CancellationToken.None);

        Assert.Equal([InstallationEnforcementState.Suspended, InstallationEnforcementState.Allowed], raisedStates);
    }

    private static async Task<InstallationEnforcementStateService> CreateInitializedServiceAsync()
    {
        var (service, _) = await CreateInitializedServiceWithStoreAsync();
        return service;
    }

    private static async Task<(InstallationEnforcementStateService Service, FakeInstallationEnforcementStateStore Store)>
        CreateInitializedServiceWithStoreAsync()
    {
        var store = new FakeInstallationEnforcementStateStore();
        var service = new InstallationEnforcementStateService(store);
        await service.InitializeAsync(CancellationToken.None);

        return (service, store);
    }
}
