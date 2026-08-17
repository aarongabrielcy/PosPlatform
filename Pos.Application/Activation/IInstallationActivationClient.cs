namespace Pos.Application.Activation;

// Puerto hacia pos-cloud para el canje del código de activación (Enrollment Code) por una
// Installation Credential. Implementado en Pos.Infrastructure mediante HTTP; esta interfaz no
// debe filtrar detalles de transporte (rutas, encabezados, DTOs HTTP) hacia Pos.Application.
public interface IInstallationActivationClient
{
    Task<InstallationEnrollmentClientResult> EnrollAsync(string enrollmentCode, CancellationToken cancellationToken);
}
