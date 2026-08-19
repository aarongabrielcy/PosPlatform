namespace Pos.Application.Enforcement;

// Persiste el estado de enforcement confirmado por el backend para que Suspended, CredentialInvalid
// y Decommissioned sobrevivan un reinicio del proceso (ver sección 13 de la tarea). Allowed se
// representa por ausencia de archivo, no por un valor persistido explícito (ver ClearAsync).
//
// Fail-safe de lectura (sección 14): si el archivo existe pero no puede leerse de forma confiable
// (corrupto, JSON inválido, error de E/S), la implementación NUNCA debe devolver null (lo que el
// llamador interpretaría como Allowed). Debe devolver Suspended como valor determinístico de
// respaldo: es el más restrictivo reversible sin intervención administrativa, y un heartbeat
// exitoso posterior lo corrige a Allowed, mientras que uno que confirme CredentialInvalid o
// Decommissioned lo corrige a ese estado real.
public interface IInstallationEnforcementStateStore
{
    Task<InstallationEnforcementState?> TryLoadAsync(CancellationToken cancellationToken);

    // state no debe ser Allowed: para volver a Allowed use ClearAsync.
    Task<bool> SaveAsync(InstallationEnforcementState state, CancellationToken cancellationToken);

    Task<bool> ClearAsync(CancellationToken cancellationToken);
}
