using Pos.Domain.Common.Identifiers;

namespace Pos.Application.Users.UserManagement;

// BASIC-USR-01: administración local de operadores (OWNER/ADMIN, MANAGER, CASHIER). Todas las
// operaciones exigen Permission.ManageUsers (sección 6/26 de la tarea) - no existe una versión de
// solo lectura para roles inferiores.
public interface IUserManagementService
{
    Task<IReadOnlyList<UserListItem>> GetUsersAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoleOption>> GetAssignableRolesAsync(CancellationToken cancellationToken = default);

    Task<CreateUserResult> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default);

    Task<UserOperationResult> UpdateUserAsync(UpdateUserRequest request, CancellationToken cancellationToken = default);

    Task<UserOperationResult> SetActiveAsync(
        UserId userId, bool isActive, CancellationToken cancellationToken = default);

    Task<UserOperationResult> ResetPasswordAsync(
        ResetPasswordRequest request, CancellationToken cancellationToken = default);
}
