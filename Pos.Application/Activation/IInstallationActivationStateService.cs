namespace Pos.Application.Activation;

// Orquesta la activación de esta Installation ante pos-cloud y expone el estado local resultante.
// No confundir con Pos.Application.Installation.IInstallationStateService, que determina si el
// negocio local (organización/sucursal/caja/administrador) ya fue configurado: son conceptos
// independientes y deliberadamente distintos (ver sección 5 de la tarea).
public interface IInstallationActivationStateService
{
    Task<ActivationStatus> GetActivationStatusAsync(CancellationToken cancellationToken);

    Task<EnrollmentOutcome> EnrollAsync(string enrollmentCode, CancellationToken cancellationToken);

    // Recuperación de credencial (secciones 6-8 de la tarea): canjea un Recovery Enrollment Code
    // emitido por el administrador ante el MISMO endpoint de enrollment que EnrollAsync. La
    // Installation conserva su InstallationId; solo se reemplaza la Installation Credential local.
    // Solo limpia el estado de enforcement CredentialInvalid después de que la nueva credencial se
    // haya persistido con éxito (nunca antes: sección 8).
    Task<EnrollmentOutcome> RecoverCredentialAsync(string recoveryEnrollmentCode, CancellationToken cancellationToken);

    // Reactivación como nueva Installation (secciones 9-11 de la tarea): canjea un Enrollment Code
    // nuevo emitido para una Installation distinta. Reemplaza tanto la Installation Credential como
    // el InstallationId local; los datos de negocio en SQLite no se tocan. Solo limpia CUALQUIER
    // estado de enforcement (incluido Decommissioned) después de que credencial y registro se hayan
    // persistido con éxito (sección 11).
    Task<EnrollmentOutcome> ActivateAsNewInstallationAsync(string enrollmentCode, CancellationToken cancellationToken);
}
