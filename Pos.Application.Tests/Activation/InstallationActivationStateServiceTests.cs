using Pos.Application.Activation;
using Pos.Application.Enforcement;
using Pos.Application.Tests.Common.Time;
using Pos.Application.Tests.Enforcement;

namespace Pos.Application.Tests.Activation;

public class InstallationActivationStateServiceTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 8, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task BlankEnrollmentCodeIsRejectedLocallyWithoutCallingTheClient()
    {
        var client = new FakeInstallationActivationClient(InstallationEnrollmentClientResult.Success("inst-1", "cred-1"));
        var credentialStore = new FakeInstallationCredentialStore();
        var recordStore = new FakeInstallationActivationRecordStore();
        var service = CreateService(client, credentialStore, recordStore);

        var outcome = await service.EnrollAsync("   ", CancellationToken.None);

        Assert.Equal(EnrollmentOutcomeStatus.InvalidInput, outcome.Status);
        Assert.Equal(0, client.CallCount);
        Assert.Equal(0, credentialStore.SaveCallCount);
        Assert.Equal(0, recordStore.SaveCallCount);
    }

    [Fact]
    public async Task ValidCodeActivatesAndPersistsCredentialThenRecord()
    {
        var client = new FakeInstallationActivationClient(InstallationEnrollmentClientResult.Success("inst-1", "cred-1"));
        var credentialStore = new FakeInstallationCredentialStore();
        var recordStore = new FakeInstallationActivationRecordStore();
        var service = CreateService(client, credentialStore, recordStore);

        var outcome = await service.EnrollAsync("  abc-123.def  ", CancellationToken.None);

        Assert.Equal(EnrollmentOutcomeStatus.Activated, outcome.Status);
        Assert.Equal(1, client.CallCount);
        Assert.Equal("abc-123.def", client.LastEnrollmentCode);
        Assert.Equal(1, credentialStore.SaveCallCount);
        Assert.Equal("cred-1", credentialStore.LastSavedCredential);
        Assert.Equal(1, recordStore.SaveCallCount);
        Assert.Equal("inst-1", recordStore.LastSavedRecord!.InstallationId);
        Assert.Equal(FixedUtcNow, recordStore.LastSavedRecord!.ActivatedAtUtc);

        Assert.Equal(ActivationStatus.Activated, await service.GetActivationStatusAsync(CancellationToken.None));
    }

    [Fact]
    public async Task RejectedEnrollmentDoesNotPersistAnything()
    {
        var client = new FakeInstallationActivationClient(InstallationEnrollmentClientResult.Rejected());
        var credentialStore = new FakeInstallationCredentialStore();
        var recordStore = new FakeInstallationActivationRecordStore();
        var service = CreateService(client, credentialStore, recordStore);

        var outcome = await service.EnrollAsync("bad-code", CancellationToken.None);

        Assert.Equal(EnrollmentOutcomeStatus.EnrollmentRejected, outcome.Status);
        Assert.Equal(0, credentialStore.SaveCallCount);
        Assert.Equal(0, recordStore.SaveCallCount);
        Assert.Equal(ActivationStatus.NotActivated, await service.GetActivationStatusAsync(CancellationToken.None));
    }

    [Fact]
    public async Task NetworkFailureDoesNotPersistAnything()
    {
        var client = new FakeInstallationActivationClient(InstallationEnrollmentClientResult.NetworkFailure());
        var credentialStore = new FakeInstallationCredentialStore();
        var recordStore = new FakeInstallationActivationRecordStore();
        var service = CreateService(client, credentialStore, recordStore);

        var outcome = await service.EnrollAsync("some-code", CancellationToken.None);

        Assert.Equal(EnrollmentOutcomeStatus.NetworkFailure, outcome.Status);
        Assert.Equal(0, credentialStore.SaveCallCount);
        Assert.Equal(0, recordStore.SaveCallCount);
        Assert.Equal(ActivationStatus.NotActivated, await service.GetActivationStatusAsync(CancellationToken.None));
    }

    // Cubre la sección 15/31 de la tarea: el Enrollment Code ya fue consumido por el backend, pero
    // la credencial no pudo guardarse localmente. No debe intentarse guardar el registro no
    // secreto ni reportarse activación exitosa.
    [Fact]
    public async Task CredentialPersistenceFailureReportsLocalPersistenceFailedAndSkipsRecordSave()
    {
        var client = new FakeInstallationActivationClient(InstallationEnrollmentClientResult.Success("inst-1", "cred-1"));
        var credentialStore = new FakeInstallationCredentialStore(saveSucceeds: false);
        var recordStore = new FakeInstallationActivationRecordStore();
        var service = CreateService(client, credentialStore, recordStore);

        var outcome = await service.EnrollAsync("some-code", CancellationToken.None);

        Assert.Equal(EnrollmentOutcomeStatus.LocalPersistenceFailed, outcome.Status);
        Assert.Equal(1, credentialStore.SaveCallCount);
        Assert.Equal(0, recordStore.SaveCallCount);
        Assert.Equal(ActivationStatus.NotActivated, await service.GetActivationStatusAsync(CancellationToken.None));
    }

    [Fact]
    public async Task RecordPersistenceFailureReportsLocalPersistenceFailedEvenThoughCredentialWasSaved()
    {
        var client = new FakeInstallationActivationClient(InstallationEnrollmentClientResult.Success("inst-1", "cred-1"));
        var credentialStore = new FakeInstallationCredentialStore();
        var recordStore = new FakeInstallationActivationRecordStore(saveSucceeds: false);
        var service = CreateService(client, credentialStore, recordStore);

        var outcome = await service.EnrollAsync("some-code", CancellationToken.None);

        Assert.Equal(EnrollmentOutcomeStatus.LocalPersistenceFailed, outcome.Status);
        Assert.Equal(1, credentialStore.SaveCallCount);
        Assert.Equal(1, recordStore.SaveCallCount);

        // La credencial sí quedó guardada, pero sin el registro no secreto el estado se sigue
        // reportando como NotActivated (fail-safe: ver GetActivationStatusAsync).
        Assert.Equal(ActivationStatus.NotActivated, await service.GetActivationStatusAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ActivationStatusRequiresBothRecordAndCredentialToBePresent()
    {
        var credentialStore = new FakeInstallationCredentialStore();
        var recordStore = new FakeInstallationActivationRecordStore(
            initialRecord: new InstallationActivationRecord("inst-1", FixedUtcNow));
        var service = CreateService(
            new FakeInstallationActivationClient(InstallationEnrollmentClientResult.NetworkFailure()),
            credentialStore,
            recordStore);

        Assert.Equal(ActivationStatus.NotActivated, await service.GetActivationStatusAsync(CancellationToken.None));
    }

    // ---------- Recuperación de credencial (secciones 6-8/35 de la tarea) ----------

    [Fact]
    public async Task RecoverCredentialWithValidCodeStoresNewCredentialKeepsInstallationIdAndClearsEnforcement()
    {
        var client = new FakeInstallationActivationClient(InstallationEnrollmentClientResult.Success("inst-1", "cred-recovered"));
        var credentialStore = new FakeInstallationCredentialStore(initialCredential: "cred-old");
        var recordStore = new FakeInstallationActivationRecordStore(
            initialRecord: new InstallationActivationRecord("inst-1", FixedUtcNow.AddDays(-30)));
        var enforcementStateService = new FakeInstallationEnforcementStateService(InstallationEnforcementState.CredentialInvalid);
        var service = CreateService(client, credentialStore, recordStore, enforcementStateService);

        var outcome = await service.RecoverCredentialAsync("recovery-code", CancellationToken.None);

        Assert.Equal(EnrollmentOutcomeStatus.Activated, outcome.Status);
        Assert.Equal(1, credentialStore.SaveCallCount);
        Assert.Equal("cred-recovered", credentialStore.LastSavedCredential);
        // El registro no secreto no se vuelve a guardar: la Installation conserva su identidad.
        Assert.Equal(0, recordStore.SaveCallCount);
        Assert.Equal(1, enforcementStateService.ClearCallCount);
        Assert.Equal(InstallationEnforcementState.Allowed, enforcementStateService.Current);
    }

    [Fact]
    public async Task RecoverCredentialWithRejectedCodeDoesNotClearEnforcement()
    {
        var client = new FakeInstallationActivationClient(InstallationEnrollmentClientResult.Rejected());
        var credentialStore = new FakeInstallationCredentialStore(initialCredential: "cred-old");
        var recordStore = new FakeInstallationActivationRecordStore(
            initialRecord: new InstallationActivationRecord("inst-1", FixedUtcNow));
        var enforcementStateService = new FakeInstallationEnforcementStateService(InstallationEnforcementState.CredentialInvalid);
        var service = CreateService(client, credentialStore, recordStore, enforcementStateService);

        var outcome = await service.RecoverCredentialAsync("bad-code", CancellationToken.None);

        Assert.Equal(EnrollmentOutcomeStatus.EnrollmentRejected, outcome.Status);
        Assert.Equal(0, credentialStore.SaveCallCount);
        Assert.Equal(0, enforcementStateService.ClearCallCount);
        Assert.Equal(InstallationEnforcementState.CredentialInvalid, enforcementStateService.Current);
    }

    // Sección 8/35 de la tarea: si la persistencia local de la credencial falla, la operación NO
    // se reporta como exitosa y el estado de enforcement sigue restringido.
    [Fact]
    public async Task RecoverCredentialWithFailedLocalPersistenceStaysRestricted()
    {
        var client = new FakeInstallationActivationClient(InstallationEnrollmentClientResult.Success("inst-1", "cred-recovered"));
        var credentialStore = new FakeInstallationCredentialStore(saveSucceeds: false, initialCredential: "cred-old");
        var recordStore = new FakeInstallationActivationRecordStore(
            initialRecord: new InstallationActivationRecord("inst-1", FixedUtcNow));
        var enforcementStateService = new FakeInstallationEnforcementStateService(InstallationEnforcementState.CredentialInvalid);
        var service = CreateService(client, credentialStore, recordStore, enforcementStateService);

        var outcome = await service.RecoverCredentialAsync("recovery-code", CancellationToken.None);

        Assert.Equal(EnrollmentOutcomeStatus.LocalPersistenceFailed, outcome.Status);
        Assert.Equal(0, enforcementStateService.ClearCallCount);
        Assert.Equal(InstallationEnforcementState.CredentialInvalid, enforcementStateService.Current);
    }

    // ---------- Activación como nueva Installation (secciones 9-11/36 de la tarea) ----------

    [Fact]
    public async Task ActivateAsNewInstallationReplacesInstallationIdAndCredentialAndClearsEnforcement()
    {
        var client = new FakeInstallationActivationClient(InstallationEnrollmentClientResult.Success("inst-2-new", "cred-new"));
        var credentialStore = new FakeInstallationCredentialStore(initialCredential: "cred-old");
        var recordStore = new FakeInstallationActivationRecordStore(
            initialRecord: new InstallationActivationRecord("inst-1-decommissioned", FixedUtcNow.AddDays(-90)));
        var enforcementStateService = new FakeInstallationEnforcementStateService(InstallationEnforcementState.Decommissioned);
        var service = CreateService(client, credentialStore, recordStore, enforcementStateService);

        var outcome = await service.ActivateAsNewInstallationAsync("new-enrollment-code", CancellationToken.None);

        Assert.Equal(EnrollmentOutcomeStatus.Activated, outcome.Status);
        Assert.Equal(1, credentialStore.SaveCallCount);
        Assert.Equal("cred-new", credentialStore.LastSavedCredential);
        Assert.Equal(1, recordStore.SaveCallCount);
        Assert.Equal("inst-2-new", recordStore.LastSavedRecord!.InstallationId);
        Assert.Equal(1, enforcementStateService.ClearCallCount);
        Assert.Equal(InstallationEnforcementState.Allowed, enforcementStateService.Current);
    }

    [Fact]
    public async Task ActivateAsNewInstallationWithInvalidCodeStaysDecommissioned()
    {
        var client = new FakeInstallationActivationClient(InstallationEnrollmentClientResult.Rejected());
        var credentialStore = new FakeInstallationCredentialStore(initialCredential: "cred-old");
        var recordStore = new FakeInstallationActivationRecordStore(
            initialRecord: new InstallationActivationRecord("inst-1-decommissioned", FixedUtcNow));
        var enforcementStateService = new FakeInstallationEnforcementStateService(InstallationEnforcementState.Decommissioned);
        var service = CreateService(client, credentialStore, recordStore, enforcementStateService);

        var outcome = await service.ActivateAsNewInstallationAsync("bad-code", CancellationToken.None);

        Assert.Equal(EnrollmentOutcomeStatus.EnrollmentRejected, outcome.Status);
        Assert.Equal(0, recordStore.SaveCallCount);
        Assert.Null(recordStore.LastSavedRecord);
        Assert.Equal(0, enforcementStateService.ClearCallCount);
        Assert.Equal(InstallationEnforcementState.Decommissioned, enforcementStateService.Current);
    }

    // Sección 11/36 de la tarea: si la credencial se persiste pero el registro no secreto falla,
    // no debe reportarse activación exitosa ni limpiarse el enforcement (identidad a medio
    // reemplazar evitada).
    [Fact]
    public async Task ActivateAsNewInstallationWithFailedRecordPersistenceStaysRestricted()
    {
        var client = new FakeInstallationActivationClient(InstallationEnrollmentClientResult.Success("inst-2-new", "cred-new"));
        var credentialStore = new FakeInstallationCredentialStore(initialCredential: "cred-old");
        var recordStore = new FakeInstallationActivationRecordStore(
            saveSucceeds: false, initialRecord: new InstallationActivationRecord("inst-1-decommissioned", FixedUtcNow));
        var enforcementStateService = new FakeInstallationEnforcementStateService(InstallationEnforcementState.Decommissioned);
        var service = CreateService(client, credentialStore, recordStore, enforcementStateService);

        var outcome = await service.ActivateAsNewInstallationAsync("new-enrollment-code", CancellationToken.None);

        Assert.Equal(EnrollmentOutcomeStatus.LocalPersistenceFailed, outcome.Status);
        Assert.Equal(0, enforcementStateService.ClearCallCount);
        Assert.Equal(InstallationEnforcementState.Decommissioned, enforcementStateService.Current);
    }

    private static InstallationActivationStateService CreateService(
        FakeInstallationActivationClient client,
        FakeInstallationCredentialStore credentialStore,
        FakeInstallationActivationRecordStore recordStore,
        FakeInstallationEnforcementStateService? enforcementStateService = null) =>
        new(client, credentialStore, recordStore, enforcementStateService ?? new FakeInstallationEnforcementStateService(), new FakeClock(FixedUtcNow));
}
