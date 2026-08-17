using Pos.Application.Activation;
using Pos.Application.Common.Time;
using Pos.Application.Common.Versioning;

namespace Pos.Application.InstallationHealth;

public sealed class InstallationHeartbeatSender : IInstallationHeartbeatSender
{
    private readonly IInstallationActivationRecordStore _recordStore;
    private readonly IInstallationCredentialStore _credentialStore;
    private readonly IInstallationHealthClient _client;
    private readonly IApplicationVersionProvider _versionProvider;
    private readonly IClock _clock;

    public InstallationHeartbeatSender(
        IInstallationActivationRecordStore recordStore,
        IInstallationCredentialStore credentialStore,
        IInstallationHealthClient client,
        IApplicationVersionProvider versionProvider,
        IClock clock)
    {
        _recordStore = recordStore ?? throw new ArgumentNullException(nameof(recordStore));
        _credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _versionProvider = versionProvider ?? throw new ArgumentNullException(nameof(versionProvider));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    // Comprueba activación y credencial por separado (en vez de reutilizar
    // IInstallationActivationStateService.GetActivationStatusAsync, que las conflates en un único
    // ActivationStatus) para poder distinguir "nunca activada" de "activada pero sin credencial
    // local" (ver sección 11 de la tarea): ambos casos deben omitir el heartbeat, pero solo el
    // segundo es una anomalía que vale la pena registrar como advertencia.
    public async Task<InstallationHeartbeatSendOutcome> SendHeartbeatAsync(CancellationToken cancellationToken)
    {
        var record = await _recordStore.TryLoadAsync(cancellationToken).ConfigureAwait(false);
        if (record is null)
        {
            return InstallationHeartbeatSendOutcome.NotActivated;
        }

        var credential = await _credentialStore.TryLoadAsync(cancellationToken).ConfigureAwait(false);
        if (credential is null)
        {
            return InstallationHeartbeatSendOutcome.CredentialMissing;
        }

        var appVersion = _versionProvider.GetVersion();

        var result = await _client
            .SendHeartbeatAsync(credential, appVersion, _clock.UtcNow, cancellationToken)
            .ConfigureAwait(false);

        return result.Status switch
        {
            InstallationHeartbeatClientStatus.Success => InstallationHeartbeatSendOutcome.Success,
            InstallationHeartbeatClientStatus.CredentialInvalid => InstallationHeartbeatSendOutcome.CredentialInvalid,
            InstallationHeartbeatClientStatus.Suspended => InstallationHeartbeatSendOutcome.Suspended,
            InstallationHeartbeatClientStatus.Decommissioned => InstallationHeartbeatSendOutcome.Decommissioned,
            _ => InstallationHeartbeatSendOutcome.NetworkFailure,
        };
    }
}
