using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;

namespace Pos.Domain.Security;

public sealed class Role
{
    private readonly HashSet<Permission> _permissions;

    public RoleId Id { get; }

    public OrganizationId OrganizationId { get; }

    public string Name { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public IReadOnlyCollection<Permission> Permissions => _permissions.ToList().AsReadOnly();

    public Role(
        RoleId id,
        OrganizationId organizationId,
        string name,
        DateTimeOffset createdAtUtc,
        IEnumerable<Permission>? permissions = null)
        : this(id, organizationId, name, true, createdAtUtc, permissions)
    {
    }

    private Role(
        RoleId id,
        OrganizationId organizationId,
        string name,
        bool isActive,
        DateTimeOffset createdAtUtc,
        IEnumerable<Permission>? permissions)
    {
        Id = EnsureNotEmpty(id);
        OrganizationId = EnsureNotEmpty(organizationId);
        Name = NormalizeName(name);
        CreatedAtUtc = EnsureUtc(createdAtUtc);
        IsActive = isActive;
        _permissions = permissions is null ? [] : [.. permissions];
    }

    // Reconstruye estado ya persistido, incluyendo IsActive y permisos, sin pasar por Activate/Deactivate/GrantPermission.
    public static Role Rehydrate(
        RoleId id,
        OrganizationId organizationId,
        string name,
        bool isActive,
        DateTimeOffset createdAtUtc,
        IEnumerable<Permission>? permissions = null) =>
        new(id, organizationId, name, isActive, createdAtUtc, permissions);

    public void Rename(string name)
    {
        Name = NormalizeName(name);
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void GrantPermission(Permission permission)
    {
        _permissions.Add(permission);
    }

    public void RevokePermission(Permission permission)
    {
        _permissions.Remove(permission);
    }

    public bool HasPermission(Permission permission)
    {
        return _permissions.Contains(permission);
    }

    private static RoleId EnsureNotEmpty(RoleId id)
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

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("Name es obligatorio.");
        }

        var trimmed = name.Trim();

        if (trimmed.Length is < 2 or > 80)
        {
            throw new DomainValidationException("Name debe tener entre 2 y 80 caracteres.");
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
