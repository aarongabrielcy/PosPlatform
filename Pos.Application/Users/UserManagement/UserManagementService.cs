using Pos.Application.Authentication;
using Pos.Application.Common.Persistence;
using Pos.Application.Common.Time;
using Pos.Application.Installation;
using Pos.Application.Security;
using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;
using Pos.Domain.Users;

namespace Pos.Application.Users.UserManagement;

// BASIC-USR-01: implementación única de administración de usuarios locales, reutilizando el
// modelo Domain/Application ya existente (User/Role/Permission/IPasswordHasher) - ninguna
// infraestructura paralela de sesión/autenticación/permisos (sección 4 de la tarea). Toda
// operación exige Permission.ManageUsers, igual patrón que ProductManagementService exige
// ManageProducts.
public sealed class UserManagementService : IUserManagementService
{
    private const int MinPasswordLength = 8;
    private const int MaxPasswordLength = 256;

    private readonly ICurrentUserSession _currentUserSession;
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public UserManagementService(
        ICurrentUserSession currentUserSession,
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _currentUserSession = currentUserSession ?? throw new ArgumentNullException(nameof(currentUserSession));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _roleRepository = roleRepository ?? throw new ArgumentNullException(nameof(roleRepository));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<IReadOnlyList<UserListItem>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        var currentUser = _currentUserSession.CurrentUser;

        if (currentUser is null || !currentUser.HasPermission(Permission.ManageUsers))
        {
            return Array.Empty<UserListItem>();
        }

        var (users, rolesById) = await LoadUsersAndRolesAsync(currentUser.OrganizationId, cancellationToken);

        return users
            .Select(u => ToListItem(u, rolesById))
            .Where(item => item is not null)
            .Select(item => item!)
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<RoleOption>> GetAssignableRolesAsync(CancellationToken cancellationToken = default)
    {
        var currentUser = _currentUserSession.CurrentUser;

        if (currentUser is null || !currentUser.HasPermission(Permission.ManageUsers))
        {
            return Array.Empty<RoleOption>();
        }

        var roles = await _roleRepository.GetByOrganizationAsync(currentUser.OrganizationId, cancellationToken);

        return roles
            .Where(role => role.IsActive)
            .OrderBy(role => role.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(role => new RoleOption(role.Id, role.Name))
            .ToList();
    }

    public async Task<CreateUserResult> CreateUserAsync(
        CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var currentUser = _currentUserSession.CurrentUser;

        if (currentUser is null)
        {
            return CreateUserResult.Failure(CreateUserResultStatus.NotAuthenticated);
        }

        if (!currentUser.HasPermission(Permission.ManageUsers))
        {
            return CreateUserResult.Failure(CreateUserResultStatus.NotAuthorized);
        }

        if (string.IsNullOrWhiteSpace(request.Username))
        {
            return CreateUserResult.Failure(CreateUserResultStatus.InvalidUsername);
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return CreateUserResult.Failure(CreateUserResultStatus.InvalidDisplayName);
        }

        if (!IsValidPasswordLength(request.Password))
        {
            return CreateUserResult.Failure(CreateUserResultStatus.InvalidPassword);
        }

        var role = await _roleRepository.GetByIdAsync(request.RoleId, cancellationToken);

        if (role is null || role.OrganizationId != currentUser.OrganizationId || !role.IsActive)
        {
            return CreateUserResult.Failure(CreateUserResultStatus.InvalidRole);
        }

        var duplicate = await _userRepository.GetByUsernameAsync(
            currentUser.OrganizationId, NormalizeUsername(request.Username), cancellationToken);

        if (duplicate is not null)
        {
            return CreateUserResult.Failure(CreateUserResultStatus.DuplicateUsername);
        }

        User newUser;

        try
        {
            var passwordHash = new PasswordHash(_passwordHasher.Hash(request.Password));

            newUser = new User(
                UserId.New(),
                currentUser.OrganizationId,
                request.RoleId,
                request.Username,
                request.DisplayName,
                passwordHash,
                _clock.UtcNow);
        }
        catch (DomainValidationException ex) when (ex.Message.StartsWith("Username", StringComparison.Ordinal))
        {
            return CreateUserResult.Failure(CreateUserResultStatus.InvalidUsername);
        }
        catch (DomainValidationException)
        {
            return CreateUserResult.Failure(CreateUserResultStatus.InvalidDisplayName);
        }

        await _userRepository.AddAsync(newUser, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        return CreateUserResult.SuccessResult(
            new UserListItem(newUser.Id, newUser.Username, newUser.DisplayName, role.Id, role.Name, newUser.IsActive));
    }

    public async Task<UserOperationResult> UpdateUserAsync(
        UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var currentUser = _currentUserSession.CurrentUser;

        if (currentUser is null)
        {
            return UserOperationResult.Failure(UserOperationResultStatus.NotAuthenticated);
        }

        if (!currentUser.HasPermission(Permission.ManageUsers))
        {
            return UserOperationResult.Failure(UserOperationResultStatus.NotAuthorized);
        }

        if (string.IsNullOrWhiteSpace(request.Username))
        {
            return UserOperationResult.Failure(UserOperationResultStatus.InvalidUsername);
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return UserOperationResult.Failure(UserOperationResultStatus.InvalidDisplayName);
        }

        var target = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);

        if (target is null || target.OrganizationId != currentUser.OrganizationId)
        {
            return UserOperationResult.Failure(UserOperationResultStatus.UserNotFound);
        }

        var newRole = await _roleRepository.GetByIdAsync(request.RoleId, cancellationToken);

        if (newRole is null || newRole.OrganizationId != currentUser.OrganizationId || !newRole.IsActive)
        {
            return UserOperationResult.Failure(UserOperationResultStatus.InvalidRole);
        }

        var duplicate = await _userRepository.GetByUsernameAsync(
            currentUser.OrganizationId, NormalizeUsername(request.Username), cancellationToken);

        if (duplicate is not null && duplicate.Id != target.Id)
        {
            return UserOperationResult.Failure(UserOperationResultStatus.DuplicateUsername);
        }

        if (target.IsActive && target.RoleId != request.RoleId)
        {
            var (_, rolesById) = await LoadUsersAndRolesAsync(currentUser.OrganizationId, cancellationToken);

            var wasTopTier = rolesById.TryGetValue(target.RoleId, out var currentRole) && IsTopTierRole(currentRole);
            var willBeTopTier = IsTopTierRole(newRole);

            if (wasTopTier && !willBeTopTier)
            {
                var remainingActiveTopTier = await CountActiveTopTierUsersExcludingAsync(
                    currentUser.OrganizationId, target.Id, cancellationToken);

                if (remainingActiveTopTier == 0)
                {
                    return UserOperationResult.Failure(UserOperationResultStatus.CannotDemoteLastAdmin);
                }
            }
        }

        try
        {
            target.ChangeUsername(request.Username);
            target.ChangeDisplayName(request.DisplayName);
            target.ChangeRole(request.RoleId);
        }
        catch (DomainValidationException ex) when (ex.Message.StartsWith("Username", StringComparison.Ordinal))
        {
            return UserOperationResult.Failure(UserOperationResultStatus.InvalidUsername);
        }
        catch (DomainValidationException)
        {
            return UserOperationResult.Failure(UserOperationResultStatus.InvalidDisplayName);
        }

        await _userRepository.UpdateAsync(target, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        return UserOperationResult.SuccessResult(
            new UserListItem(target.Id, target.Username, target.DisplayName, newRole.Id, newRole.Name, target.IsActive));
    }

    public async Task<UserOperationResult> SetActiveAsync(
        UserId userId, bool isActive, CancellationToken cancellationToken = default)
    {
        var currentUser = _currentUserSession.CurrentUser;

        if (currentUser is null)
        {
            return UserOperationResult.Failure(UserOperationResultStatus.NotAuthenticated);
        }

        if (!currentUser.HasPermission(Permission.ManageUsers))
        {
            return UserOperationResult.Failure(UserOperationResultStatus.NotAuthorized);
        }

        var target = await _userRepository.GetByIdAsync(userId, cancellationToken);

        if (target is null || target.OrganizationId != currentUser.OrganizationId)
        {
            return UserOperationResult.Failure(UserOperationResultStatus.UserNotFound);
        }

        var role = await _roleRepository.GetByIdAsync(target.RoleId, cancellationToken);

        if (role is null)
        {
            return UserOperationResult.Failure(UserOperationResultStatus.UserNotFound);
        }

        // Última protección de nivel OWNER/ADMIN (sección 13/14 de la tarea): desactivar solo se
        // bloquea cuando el usuario objetivo está actualmente activo y en un Role de nivel
        // completo, y no queda ningún otro usuario activo equivalente.
        if (!isActive && target.IsActive && IsTopTierRole(role))
        {
            var remainingActiveTopTier = await CountActiveTopTierUsersExcludingAsync(
                currentUser.OrganizationId, target.Id, cancellationToken);

            if (remainingActiveTopTier == 0)
            {
                return UserOperationResult.Failure(UserOperationResultStatus.CannotDeactivateLastAdmin);
            }
        }

        if (isActive)
        {
            target.Activate();
        }
        else
        {
            target.Deactivate();
        }

        await _userRepository.UpdateAsync(target, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        return UserOperationResult.SuccessResult(
            new UserListItem(target.Id, target.Username, target.DisplayName, role.Id, role.Name, target.IsActive));
    }

    public async Task<UserOperationResult> ResetPasswordAsync(
        ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var currentUser = _currentUserSession.CurrentUser;

        if (currentUser is null)
        {
            return UserOperationResult.Failure(UserOperationResultStatus.NotAuthenticated);
        }

        if (!currentUser.HasPermission(Permission.ManageUsers))
        {
            return UserOperationResult.Failure(UserOperationResultStatus.NotAuthorized);
        }

        if (!IsValidPasswordLength(request.NewPassword))
        {
            return UserOperationResult.Failure(UserOperationResultStatus.InvalidPassword);
        }

        var target = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);

        if (target is null || target.OrganizationId != currentUser.OrganizationId)
        {
            return UserOperationResult.Failure(UserOperationResultStatus.UserNotFound);
        }

        var role = await _roleRepository.GetByIdAsync(target.RoleId, cancellationToken);

        if (role is null)
        {
            return UserOperationResult.Failure(UserOperationResultStatus.UserNotFound);
        }

        var passwordHash = new PasswordHash(_passwordHasher.Hash(request.NewPassword));
        target.ChangePasswordHash(passwordHash);

        await _userRepository.UpdateAsync(target, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        return UserOperationResult.SuccessResult(
            new UserListItem(target.Id, target.Username, target.DisplayName, role.Id, role.Name, target.IsActive));
    }

    // Mismo criterio que AuthenticationService.NormalizeUsername (sección 10 de la tarea:
    // "no silently duplicated by casing/whitespace") - debe aplicarse ANTES de comparar contra
    // IUserRepository.GetByUsernameAsync, que hace coincidencia exacta: sin esto, "Cajero1" no
    // encontraría un "CAJERO1" ya persistido (User.NormalizeUsername siempre almacena en
    // mayúsculas), dejando pasar un duplicado real hasta el índice único de base de datos.
    private static string NormalizeUsername(string username) => username.Trim().ToUpperInvariant();

    private async Task<(IReadOnlyList<User> Users, Dictionary<RoleId, Role> RolesById)> LoadUsersAndRolesAsync(
        OrganizationId organizationId, CancellationToken cancellationToken)
    {
        var users = await _userRepository.GetByOrganizationAsync(organizationId, cancellationToken);
        var roles = await _roleRepository.GetByOrganizationAsync(organizationId, cancellationToken);
        var rolesById = roles.ToDictionary(role => role.Id);

        return (users, rolesById);
    }

    private async Task<int> CountActiveTopTierUsersExcludingAsync(
        OrganizationId organizationId, UserId excludedUserId, CancellationToken cancellationToken)
    {
        var (users, rolesById) = await LoadUsersAndRolesAsync(organizationId, cancellationToken);

        return users.Count(u =>
            u.IsActive &&
            u.Id != excludedUserId &&
            rolesById.TryGetValue(u.RoleId, out var role) &&
            IsTopTierRole(role));
    }

    // "Nivel completo" (sección 13 de la tarea) se define exactamente igual que
    // InstallationStructureInspector.SatisfiesAdministrativePermissions: un Role administrativo es
    // aquel cuyo conjunto de permisos incluye TODOS los valores de Permission, sin comparar por
    // nombre ("Administrator"). Reutiliza AdministrativePermissionSet en vez de duplicar el criterio.
    private static bool IsTopTierRole(Role role)
    {
        var rolePermissions = new HashSet<Permission>(role.Permissions);

        return AdministrativePermissionSet.All().All(rolePermissions.Contains);
    }

    private static bool IsValidPasswordLength(string? password) =>
        !string.IsNullOrWhiteSpace(password) && password.Length is >= MinPasswordLength and <= MaxPasswordLength;

    private static UserListItem? ToListItem(User user, Dictionary<RoleId, Role> rolesById) =>
        rolesById.TryGetValue(user.RoleId, out var role)
            ? new UserListItem(user.Id, user.Username, user.DisplayName, role.Id, role.Name, user.IsActive)
            : null;
}
