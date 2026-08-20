namespace Pos.Application.Users.UserManagement;

// Estado compartido por UpdateAsync/SetActiveAsync/ResetPasswordAsync (igual patrón que
// UpdateProductResultStatus cubre Update/SetActive en ProductManagementService).
public enum UserOperationResultStatus
{
    Success,
    NotAuthenticated,
    NotAuthorized,
    UserNotFound,
    InvalidUsername,
    InvalidDisplayName,
    InvalidRole,
    InvalidPassword,
    DuplicateUsername,

    // Última protección de nivel OWNER/ADMIN (sección 13/14 de la tarea): nunca dejar la
    // instalación sin ningún usuario activo con el conjunto completo de permisos.
    CannotDeactivateLastAdmin,
    CannotDemoteLastAdmin,
}
