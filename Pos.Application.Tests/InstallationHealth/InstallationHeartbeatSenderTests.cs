using Pos.Application.Activation;
using Pos.Application.InstallationHealth;
using Pos.Application.Tests.Activation;
using Pos.Application.Tests.Common.Time;

namespace Pos.Application.Tests.InstallationHealth;

public class InstallationHeartbeatSenderTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 8, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task NoActivationRecordSkipsHeartbeatWithoutCallingTheClient()
    {
        var client = new FakeInstallationHealthClient(InstallationHeartbeatClientResult.Success());
        var recordStore = new FakeInstallationActivationRecordStore();
        var credentialStore = new FakeInstallationCredentialStore();
        var sender = CreateSender(recordStore, credentialStore, client);

        var outcome = await sender.SendHeartbeatAsync(CancellationToken.None);

        Assert.Equal(InstallationHeartbeatSendOutcome.NotActivated, outcome);
        Assert.Equal(0, client.CallCount);
    }

    // Cubre la sección 11 de la tarea: metadatos de activación presentes pero credencial ausente es
    // una anomalía distinta de "nunca activada" - ninguna de las dos debe intentar el heartbeat.
    [Fact]
    public async Task ActivationRecordWithoutCredentialSkipsHeartbeatAndReportsCredentialMissing()
    {
        var client = new FakeInstallationHealthClient(InstallationHeartbeatClientResult.Success());
        var recordStore = new FakeInstallationActivationRecordStore(
            initialRecord: new InstallationActivationRecord("inst-1", FixedUtcNow));
        var credentialStore = new FakeInstallationCredentialStore(initialCredential: null);
        var sender = CreateSender(recordStore, credentialStore, client);

        var outcome = await sender.SendHeartbeatAsync(CancellationToken.None);

        Assert.Equal(InstallationHeartbeatSendOutcome.CredentialMissing, outcome);
        Assert.Equal(0, client.CallCount);
    }

    [Fact]
    public async Task ActivatedInstallationSendsHeartbeatWithCredentialVersionAndUtcClock()
    {
        var client = new FakeInstallationHealthClient(InstallationHeartbeatClientResult.Success());
        var recordStore = new FakeInstallationActivationRecordStore(
            initialRecord: new InstallationActivationRecord("inst-1", FixedUtcNow));
        var credentialStore = new FakeInstallationCredentialStore(initialCredential: "cred-1.secret");
        var sender = CreateSender(recordStore, credentialStore, client, appVersion: "1.4.2");

        var outcome = await sender.SendHeartbeatAsync(CancellationToken.None);

        Assert.Equal(InstallationHeartbeatSendOutcome.Success, outcome);
        Assert.Equal(1, client.CallCount);
        Assert.Equal("cred-1.secret", client.LastCredential);
        Assert.Equal("1.4.2", client.LastAppVersion);
        Assert.Equal(FixedUtcNow, client.LastClientReportedAtUtc);
    }

    [Theory]
    [InlineData(InstallationHeartbeatClientStatus.CredentialInvalid, InstallationHeartbeatSendOutcome.CredentialInvalid)]
    [InlineData(InstallationHeartbeatClientStatus.Suspended, InstallationHeartbeatSendOutcome.Suspended)]
    [InlineData(InstallationHeartbeatClientStatus.Decommissioned, InstallationHeartbeatSendOutcome.Decommissioned)]
    [InlineData(InstallationHeartbeatClientStatus.NetworkFailure, InstallationHeartbeatSendOutcome.NetworkFailure)]
    public async Task ClientResultStatusMapsToTheMatchingSendOutcome(
        InstallationHeartbeatClientStatus clientStatus,
        InstallationHeartbeatSendOutcome expectedOutcome)
    {
        var client = new FakeInstallationHealthClient(new InstallationHeartbeatClientResult(clientStatus));
        var recordStore = new FakeInstallationActivationRecordStore(
            initialRecord: new InstallationActivationRecord("inst-1", FixedUtcNow));
        var credentialStore = new FakeInstallationCredentialStore(initialCredential: "cred-1.secret");
        var sender = CreateSender(recordStore, credentialStore, client);

        var outcome = await sender.SendHeartbeatAsync(CancellationToken.None);

        Assert.Equal(expectedOutcome, outcome);
    }

    private static InstallationHeartbeatSender CreateSender(
        FakeInstallationActivationRecordStore recordStore,
        FakeInstallationCredentialStore credentialStore,
        FakeInstallationHealthClient client,
        string appVersion = "1.0.0") =>
        new(
            recordStore,
            credentialStore,
            client,
            new FakeApplicationVersionProvider(appVersion),
            new FakeClock(FixedUtcNow));
}
