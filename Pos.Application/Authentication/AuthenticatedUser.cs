using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;

namespace Pos.Application.Authentication;

public sealed class AuthenticatedUser
{
    private readonly HashSet<Permission> _permissions;

    public UserId UserId { get; }

    public OrganizationId OrganizationId { get; }

    public RoleId RoleId { get; }

    public string Username { get; }

    public string DisplayName { get; }

    public string RoleName { get; }

    public IReadOnlySet<Permission> Permissions => _permissions;

    public AuthenticatedUser(
        UserId userId,
        OrganizationId organizationId,
        RoleId roleId,
        string username,
        string displayName,
        string roleName,
        IEnumerable<Permission> permissions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(roleName);
        ArgumentNullException.ThrowIfNull(permissions);

        UserId = userId;
        OrganizationId = organizationId;
        RoleId = roleId;
        Username = username;
        DisplayName = displayName;
        RoleName = roleName;
        _permissions = [.. permissions];
    }

    public bool HasPermission(Permission permission) => _permissions.Contains(permission);

    // No expone datos sensibles: solo identidad, rol y permisos.
    public override string ToString() => $"AuthenticatedUser[Username={Username}, RoleName={RoleName}]";
}
