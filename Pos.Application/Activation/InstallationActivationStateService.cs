using Pos.Application.Common.Time;
using Pos.Application.Enforcement;

namespace Pos.Application.Activation;

public sealed class InstallationActivationStateService : IInstallationActivationStateService
{
    private readonly IInstallationActivationClient _client;
    private readonly IInstallationCredentialStore _credentialStore;
    private readonly IInstallationActivationRecordStore _recordStore;
    private readonly IInstallationEnforcementStateService _enforcementStateService;
    private readonly IClock _clock;

    public InstallationActivationStateService(
        IInstallationActivationClient client,
        IInstallationCredentialStore credentialStore,
        IInstallationActivationRecordStore recordStore,
        IInstallationEnforcementStateService enforcementStateService,
        IClock clock)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
        _recordStore = recordStore ?? throw new ArgumentNullException(nameof(recordStore));
        _enforcementStateService = enforcementStateService ?? throw new ArgumentNullException(nameof(enforcementStateService));
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

    public Task<EnrollmentOutcome> EnrollAsync(string enrollmentCode, CancellationToken cancellationToken) =>
        RedeemAsync(
            enrollmentCode,
            persistRecordAsync: (clientResult, ct) => _recordStore.SaveAsync(
                new InstallationActivationRecord(clientResult.InstallationId!, _clock.UtcNow), ct),
            clearEnforcementOnSuccess: false,
            cancellationToken);

    public Task<EnrollmentOutcome> RecoverCredentialAsync(string recoveryEnrollmentCode, CancellationToken cancellationToken) =>
        RedeemAsync(
            recoveryEnrollmentCode,
            // La recuperación de credencial no cambia el InstallationId ni ActivatedAtUtc locales
            // (el backend confirma la misma Installation): solo se reemplaza la credencial, ya
            // guardada por RedeemAsync antes de llegar aquí.
            persistRecordAsync: (_, _) => Task.FromResult(true),
            clearEnforcementOnSuccess: true,
            cancellationToken);

    public Task<EnrollmentOutcome> ActivateAsNewInstallationAsync(string enrollmentCode, CancellationToken cancellationToken) =>
        RedeemAsync(
            enrollmentCode,
            persistRecordAsync: (clientResult, ct) => _recordStore.SaveAsync(
                new InstallationActivationRecord(clientResult.InstallationId!, _clock.UtcNow), ct),
            clearEnforcementOnSuccess: true,
            cancellationToken);

    // Orquestador único reutilizado por EnrollAsync/RecoverCredentialAsync/ActivateAsNewInstallationAsync
    // (sección 8 de la tarea: "no duplicar la lógica de negocio de Activation si el orquestador
    // existente puede generalizarse/reutilizarse con seguridad"). El canje HTTP es idéntico en los
    // tres flujos: solo cambia qué registro local se persiste después y si corresponde limpiar el
    // estado de enforcement al final.
    private async Task<EnrollmentOutcome> RedeemAsync(
        string code,
        Func<InstallationEnrollmentClientResult, CancellationToken, Task<bool>> persistRecordAsync,
        bool clearEnforcementOnSuccess,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return new EnrollmentOutcome(EnrollmentOutcomeStatus.InvalidInput);
        }

        var clientResult = await _client.EnrollAsync(code.Trim(), cancellationToken).ConfigureAwait(false);

        if (clientResult.Status == InstallationEnrollmentClientStatus.Rejected)
        {
            return new EnrollmentOutcome(EnrollmentOutcomeStatus.EnrollmentRejected);
        }

        if (clientResult.Status == InstallationEnrollmentClientStatus.NetworkFailure)
        {
            return new EnrollmentOutcome(EnrollmentOutcomeStatus.NetworkFailure);
        }

        // A partir de aquí el backend ya consumió el código: no hay forma de reintentar con el
        // mismo valor. La credencial debe persistir antes que el registro no secreto, y ambos deben
        // completarse antes de reportar Activated (ver sección 11 de la tarea).
        var credentialSaved = await _credentialStore
            .SaveAsync(clientResult.Credential!, cancellationToken)
            .ConfigureAwait(false);

        if (!credentialSaved)
        {
            return new EnrollmentOutcome(EnrollmentOutcomeStatus.LocalPersistenceFailed);
        }

        var recordPersisted = await persistRecordAsync(clientResult, cancellationToken).ConfigureAwait(false);

        if (!recordPersisted)
        {
            return new EnrollmentOutcome(EnrollmentOutcomeStatus.LocalPersistenceFailed);
        }

        if (clearEnforcementOnSuccess)
        {
            // Solo después de que toda la persistencia local haya tenido éxito (sección 8/11): la
            // aplicación nunca se considera recuperada/reactivada antes de este punto.
            await _enforcementStateService.ClearAsync(cancellationToken).ConfigureAwait(false);
        }

        return new EnrollmentOutcome(EnrollmentOutcomeStatus.Activated);
    }
}
