using Pos.Application.Activation;
using Pos.Application.Tests.Common.Time;

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

    private static InstallationActivationStateService CreateService(
        FakeInstallationActivationClient client,
        FakeInstallationCredentialStore credentialStore,
        FakeInstallationActivationRecordStore recordStore) =>
        new(client, credentialStore, recordStore, new FakeClock(FixedUtcNow));
}
