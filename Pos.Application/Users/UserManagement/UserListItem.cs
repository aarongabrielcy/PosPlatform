using Pos.Domain.Common.Identifiers;

namespace Pos.Application.Users.UserManagement;

// Proyección de User+Role para la pantalla de administración de usuarios. Solo primitivos (igual
// que ProductCatalogItem/ActiveRegisterSession): nunca expone PasswordHash.
public sealed class UserListItem
{
    public UserId UserId { get; }

    public string Username { get; }

    public string DisplayName { get; }

    public RoleId RoleId { get; }

    public string RoleName { get; }

    public bool IsActive { get; }

    public UserListItem(
        UserId userId, string username, string displayName, RoleId roleId, string roleName, bool isActive)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(roleName);

        UserId = userId;
        Username = username;
        DisplayName = displayName;
        RoleId = roleId;
        RoleName = roleName;
        IsActive = isActive;
    }
}
