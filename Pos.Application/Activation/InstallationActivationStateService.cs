using Pos.Application.Common.Time;

namespace Pos.Application.Activation;

public sealed class InstallationActivationStateService : IInstallationActivationStateService
{
    private readonly IInstallationActivationClient _client;
    private readonly IInstallationCredentialStore _credentialStore;
    private readonly IInstallationActivationRecordStore _recordStore;
    private readonly IClock _clock;

    public InstallationActivationStateService(
        IInstallationActivationClient client,
        IInstallationCredentialStore credentialStore,
        IInstallationActivationRecordStore recordStore,
        IClock clock)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
        _recordStore = recordStore ?? throw new ArgumentNullException(nameof(recordStore));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    // Activada solo si ambos, el registro no secreto y la credencial, están presentes: EnrollAsync
    // nunca guarda el registro sin haber guardado antes la credencial (ver comentario allí), pero
    // esta doble comprobación mantiene el estado local a prueba de fallos aunque uno de los dos
    // artefactos se pierda o corrompa por fuera de este flujo.
    public async Task<ActivationStatus> GetActivationStatusAsync(CancellationToken cancellationToken)
    {
        var record = await _recordStore.TryLoadAsync(cancellationToken).ConfigureAwait(false);
        if (record is null)
        {
            return ActivationStatus.NotActivated;
        }

        var credential = await _credentialStore.TryLoadAsync(cancellationToken).ConfigureAwait(false);
        return credential is null ? ActivationStatus.NotActivated : ActivationStatus.Activated;
    }

    public async Task<EnrollmentOutcome> EnrollAsync(string enrollmentCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(enrollmentCode))
        {
            return new EnrollmentOutcome(EnrollmentOutcomeStatus.InvalidInput);
        }

        var clientResult = await _client.EnrollAsync(enrollmentCode.Trim(), cancellationToken).ConfigureAwait(false);

        if (clientResult.Status == InstallationEnrollmentClientStatus.Rejected)
        {
            return new EnrollmentOutcome(EnrollmentOutcomeStatus.EnrollmentRejected);
        }

        if (clientResult.Status == InstallationEnrollmentClientStatus.NetworkFailure)
        {
            return new EnrollmentOutcome(EnrollmentOutcomeStatus.NetworkFailure);
        }

        // A partir de aquí el backend ya consumió el Enrollment Code: no hay forma de reintentar
        // con el mismo código. La credencial debe persistir antes que el registro no secreto, y
        // ambos deben completarse antes de reportar Activated (ver sección 15 de la tarea).
        var credentialSaved = await _credentialStore
            .SaveAsync(clientResult.Credential!, cancellationToken)
            .ConfigureAwait(false);

        if (!credentialSaved)
        {
            return new EnrollmentOutcome(EnrollmentOutcomeStatus.LocalPersistenceFailed);
        }

        var record = new InstallationActivationRecord(clientResult.InstallationId!, _clock.UtcNow);
        var recordSaved = await _recordStore.SaveAsync(record, cancellationToken).ConfigureAwait(false);

        if (!recordSaved)
        {
            return new EnrollmentOutcome(EnrollmentOutcomeStatus.LocalPersistenceFailed);
        }

        return new EnrollmentOutcome(EnrollmentOutcomeStatus.Activated);
    }
}
