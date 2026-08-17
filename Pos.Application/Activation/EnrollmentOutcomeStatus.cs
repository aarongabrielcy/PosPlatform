namespace Pos.Application.Activation;

public enum EnrollmentOutcomeStatus
{
    Activated,
    InvalidInput,
    EnrollmentRejected,
    NetworkFailure,

    // El backend consumió el Enrollment Code y devolvió una Installation Credential válida, pero
    // no fue posible persistirla localmente. El código ya no puede reutilizarse: se requiere un
    // nuevo código de recuperación emitido por el administrador (ver sección 15/24 de la tarea).
    LocalPersistenceFailed,
}
