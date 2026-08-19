using Pos.Application.InstallationHealth;

namespace Pos.Application.Enforcement;

// Implementa la tabla de transición de la sección 16 de la tarea. El estado en memoria (_current)
// se actualiza siempre de inmediato, incluso si la persistencia en IInstallationEnforcementStateStore
// falla: el propósito principal de este servicio es proteger la aplicación EN EJECUCIÓN ahora mismo
// (sección 17), no solo en el próximo reinicio. Un fallo de persistencia ya queda registrado por la
// propia implementación de IInstallationEnforcementStateStore (Pos.Infrastructure): Pos.Application
// no referencia Microsoft.Extensions.Logging (solo depende de Pos.Domain, ver CLAUDE.md sección B),
// así que este servicio no duplica ese registro.
//
// Un lock exclusivo serializa las transiciones: el heartbeat corre en un único BackgroundService
// (nunca concurrente consigo mismo), pero una recuperación de credencial o una reactivación como
// nueva Installation puede coincidir en el tiempo con un ciclo de heartbeat en curso (sección 15/16:
// "no dejar que resultados obsoletos compitan con un estado más nuevo").
public sealed class InstallationEnforcementStateService : IInstallationEnforcementStateService, IDisposable
{
    private readonly IInstallationEnforcementStateStore _store;
    private readonly SemaphoreSlim _sync = new(1, 1);

    private InstallationEnforcementState _current = InstallationEnforcementState.Allowed;
    private bool _initialized;

    public InstallationEnforcementStateService(IInstallationEnforcementStateStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public InstallationEnforcementState Current => _current;

    public event EventHandler<InstallationEnforcementState>? StateChanged;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await _sync.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_initialized)
            {
                return;
            }

            var persisted = await _store.TryLoadAsync(cancellationToken).ConfigureAwait(false);
            _current = persisted ?? InstallationEnforcementState.Allowed;
            _initialized = true;
        }
        finally
        {
            _sync.Release();
        }
    }

    public async Task ApplyHeartbeatOutcomeAsync(InstallationHeartbeatSendOutcome outcome, CancellationToken cancellationToken)
    {
        switch (outcome)
        {
            case InstallationHeartbeatSendOutcome.Success:
                // Decommissioned + Success nunca vuelve a Allowed por sí solo (sección 16): solo una
                // reactivación explícita como nueva Installation lo hace (ClearAsync). CredentialInvalid
                // tampoco se limpia aquí: solo una recuperación de credencial exitosa lo hace.
                if (_current == InstallationEnforcementState.Suspended)
                {
                    await TransitionToAllowedAsync(cancellationToken).ConfigureAwait(false);
                }
                break;

            case InstallationHeartbeatSendOutcome.Suspended:
                await TransitionToRestrictedAsync(InstallationEnforcementState.Suspended, cancellationToken).ConfigureAwait(false);
                break;

            case InstallationHeartbeatSendOutcome.CredentialInvalid:
                await TransitionToRestrictedAsync(InstallationEnforcementState.CredentialInvalid, cancellationToken).ConfigureAwait(false);
                break;

            case InstallationHeartbeatSendOutcome.Decommissioned:
                await TransitionToRestrictedAsync(InstallationEnforcementState.Decommissioned, cancellationToken).ConfigureAwait(false);
                break;

            case InstallationHeartbeatSendOutcome.NetworkFailure:
            case InstallationHeartbeatSendOutcome.NotActivated:
            case InstallationHeartbeatSendOutcome.CredentialMissing:
            default:
                // Un fallo de red nunca degrada ni mejora un estado de enforcement confirmado
                // (sección 4/25/31 de la tarea): la instalación sigue exactamente como estaba.
                break;
        }
    }

    public Task ClearAsync(CancellationToken cancellationToken) => TransitionToAllowedAsync(cancellationToken);

    private async Task TransitionToRestrictedAsync(InstallationEnforcementState state, CancellationToken cancellationToken)
    {
        await _sync.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // Se intenta persistir siempre (idempotente y barato), aunque el estado en memoria no
            // cambie: así un intento de escritura fallido anterior se reintenta en el próximo
            // heartbeat sin depender de un cambio de valor para disparar el reintento.
            await _store.SaveAsync(state, cancellationToken).ConfigureAwait(false);

            var previous = _current;
            _current = state;

            if (previous != state)
            {
                StateChanged?.Invoke(this, state);
            }
        }
        finally
        {
            _sync.Release();
        }
    }

    private async Task TransitionToAllowedAsync(CancellationToken cancellationToken)
    {
        await _sync.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await _store.ClearAsync(cancellationToken).ConfigureAwait(false);

            var previous = _current;
            _current = InstallationEnforcementState.Allowed;

            if (previous != InstallationEnforcementState.Allowed)
            {
                StateChanged?.Invoke(this, InstallationEnforcementState.Allowed);
            }
        }
        finally
        {
            _sync.Release();
        }
    }

    public void Dispose() => _sync.Dispose();
}
