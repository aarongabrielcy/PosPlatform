namespace Pos.Application.Activation;

// Orquesta la activación de esta Installation ante pos-cloud y expone el estado local resultante.
// No confundir con Pos.Application.Installation.IInstallationStateService, que determina si el
// negocio local (organización/sucursal/caja/administrador) ya fue configurado: son conceptos
// independientes y deliberadamente distintos (ver sección 5 de la tarea).
public interface IInstallationActivationStateService
{
    Task<ActivationStatus> GetActivationStatusAsync(CancellationToken cancellationToken);

    Task<EnrollmentOutcome> EnrollAsync(string enrollmentCode, CancellationToken cancellationToken);
}
