using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;

namespace Pos.Domain.Users;

public sealed class User
{
    public UserId Id { get; }

    public OrganizationId OrganizationId { get; }

    public RoleId RoleId { get; private set; }

    public string Username { get; private set; }

    public string DisplayName { get; private set; }

    public PasswordHash PasswordHash { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public User(
        UserId id,
        OrganizationId organizationId,
        RoleId roleId,
        string username,
        string displayName,
        PasswordHash passwordHash,
        DateTimeOffset createdAtUtc)
        : this(id, organizationId, roleId, username, displayName, passwordHash, true, createdAtUtc)
    {
    }

    private User(
        UserId id,
        OrganizationId organizationId,
        RoleId roleId,
        string username,
        string displayName,
        PasswordHash passwordHash,
        bool isActive,
        DateTimeOffset createdAtUtc)
    {
        Id = EnsureNotEmpty(id);
        OrganizationId = EnsureNotEmpty(organizationId);
        RoleId = EnsureNotEmpty(roleId);
        Username = NormalizeUsername(username);
        DisplayName = NormalizeDisplayName(displayName);
        PasswordHash = EnsureNotEmpty(passwordHash);
        CreatedAtUtc = EnsureUtc(createdAtUtc);
        IsActive = isActive;
    }

    // Reconstruye estado ya persistido, incluyendo IsActive, sin pasar por Activate/Deactivate.
    public static User Rehydrate(
        UserId id,
        OrganizationId organizationId,
        RoleId roleId,
        string username,
        string displayName,
        PasswordHash passwordHash,
        bool isActive,
        DateTimeOffset createdAtUtc) =>
        new(id, organizationId, roleId, username, displayName, passwordHash, isActive, createdAtUtc);

    public void ChangeDisplayName(string displayName)
    {
        DisplayName = NormalizeDisplayName(displayName);
    }

    public void ChangePasswordHash(PasswordHash passwordHash)
    {
        PasswordHash = EnsureNotEmpty(passwordHash);
    }

    public void ChangeRole(RoleId roleId)
    {
        RoleId = EnsureNotEmpty(roleId);
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    private static UserId EnsureNotEmpty(UserId id)
    {
        if (id.Value == Guid.Empty)
        {
            throw new DomainValidationException("Id no puede ser vacío.");
        }

        return id;
    }

    private static OrganizationId EnsureNotEmpty(OrganizationId organizationId)
    {
        if (organizationId.Value == Guid.Empty)
        {
            throw new DomainValidationException("OrganizationId no puede ser vacío.");
        }

        return organizationId;
    }

    private static RoleId EnsureNotEmpty(RoleId roleId)
    {
        if (roleId.Value == Guid.Empty)
        {
            throw new DomainValidationException("RoleId no puede ser vacío.");
        }

        return roleId;
    }

    private static PasswordHash EnsureNotEmpty(PasswordHash passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash.Value))
        {
            throw new DomainValidationException("PasswordHash no puede ser vacío.");
        }

        return passwordHash;
    }

    private static string NormalizeUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new DomainValidationException("Username es obligatorio.");
        }

        var normalized = username.Trim().ToUpperInvariant();

        if (normalized.Length is < 3 or > 40)
        {
            throw new DomainValidationException("Username debe tener entre 3 y 40 caracteres.");
        }

        if (!normalized.All(c => c is (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '.' or '-' or '_'))
        {
            throw new DomainValidationException(
                "Username solo puede contener letras A-Z, números 0-9, punto, guion medio y guion bajo.");
        }

        return normalized;
    }

    private static string NormalizeDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new DomainValidationException("DisplayName es obligatorio.");
        }

        var trimmed = displayName.Trim();

        if (trimmed.Length is < 2 or > 120)
        {
            throw new DomainValidationException("DisplayName debe tener entre 2 y 120 caracteres.");
        }

        return trimmed;
    }

    private static DateTimeOffset EnsureUtc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new DomainValidationException("CreatedAtUtc debe tener Offset igual a TimeSpan.Zero.");
        }

        return value;
    }
}
