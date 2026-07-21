using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;
using Pos.Infrastructure.Persistence.Exceptions;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Persistence.Mappers;

internal static class RoleMapper
{
    internal static Role ToDomain(RoleRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        try
        {
            var permissions = ParsePermissions(record);

            return Role.Rehydrate(
                new RoleId(record.Id),
                new OrganizationId(record.OrganizationId),
                record.Name,
                record.IsActive,
                record.CreatedAtUtc,
                permissions);
        }
        catch (DomainValidationException ex)
        {
            throw new PersistenceDataException(
                $"RoleRecord con Id '{record.Id}' contiene datos inválidos: {ex.Message}", ex);
        }
    }

    internal static RoleRecord ToRecord(Role role)
    {
        ArgumentNullException.ThrowIfNull(role);

        var record = new RoleRecord
        {
            Id = role.Id.Value,
            OrganizationId = role.OrganizationId.Value,
            Name = role.Name,
            IsActive = role.IsActive,
            CreatedAtUtc = role.CreatedAtUtc,
        };

        foreach (var permission in role.Permissions.Select(p => p.ToString()).OrderBy(p => p, StringComparer.Ordinal))
        {
            record.Permissions.Add(new RolePermissionRecord
            {
                RoleId = record.Id,
                Permission = permission,
                Role = record,
            });
        }

        return record;
    }

    // Sincroniza RoleRecord en dos fases: FASE A valida identidad y coherencia completa del
    // estado persistido (incluyendo cada permiso) sin mutar nada; FASE B solo se ejecuta si
    // toda la validación fue exitosa, y aplica name/is_active y sincroniza permisos por valor.
    internal static void UpdateRecord(Role role, RoleRecord record)
    {
        ArgumentNullException.ThrowIfNull(role);
        ArgumentNullException.ThrowIfNull(record);

        // ---------- FASE A: validar sin mutar ----------

        if (record.Id != role.Id.Value)
        {
            throw new PersistenceDataException(
                $"No se puede actualizar RoleRecord '{record.Id}': Id no coincide con Role '{role.Id}'.");
        }

        if (record.OrganizationId != role.OrganizationId.Value)
        {
            throw new PersistenceDataException(
                $"No se puede actualizar RoleRecord '{record.Id}': OrganizationId no coincide.");
        }

        if (record.CreatedAtUtc != role.CreatedAtUtc)
        {
            throw new PersistenceDataException(
                $"No se puede actualizar RoleRecord '{record.Id}': CreatedAtUtc no coincide.");
        }

        var seenTextualPermissions = new HashSet<string>(StringComparer.Ordinal);
        var seenSemanticPermissions = new HashSet<Permission>();

        foreach (var permissionRecord in record.Permissions)
        {
            if (permissionRecord is null)
            {
                throw new PersistenceDataException(
                    $"No se puede actualizar RoleRecord '{record.Id}': la colección de permisos contiene un elemento nulo.");
            }

            if (permissionRecord.RoleId != record.Id)
            {
                throw new PersistenceDataException(
                    $"No se puede actualizar RoleRecord '{record.Id}': permiso con RoleId '{permissionRecord.RoleId}' no coincide.");
            }

            if (string.IsNullOrWhiteSpace(permissionRecord.Permission))
            {
                throw new PersistenceDataException(
                    $"No se puede actualizar RoleRecord '{record.Id}': contiene un permiso vacío.");
            }

            if (!Enum.TryParse<Permission>(permissionRecord.Permission, out var parsedPermission)
                || !Enum.IsDefined(parsedPermission))
            {
                throw new PersistenceDataException(
                    $"No se puede actualizar RoleRecord '{record.Id}': permiso '{permissionRecord.Permission}' no es un valor válido.");
            }

            if (!seenTextualPermissions.Add(permissionRecord.Permission))
            {
                throw new PersistenceDataException(
                    $"No se puede actualizar RoleRecord '{record.Id}': permiso '{permissionRecord.Permission}' está duplicado.");
            }

            if (!seenSemanticPermissions.Add(parsedPermission))
            {
                throw new PersistenceDataException(
                    $"No se puede actualizar RoleRecord '{record.Id}': permiso '{permissionRecord.Permission}' está semánticamente duplicado.");
            }
        }

        var desiredPermissionNames = new List<string>();
        var desiredPermissionSet = new HashSet<string>(StringComparer.Ordinal);

        foreach (var permission in role.Permissions)
        {
            var name = permission.ToString();
            desiredPermissionNames.Add(name);

            if (!desiredPermissionSet.Add(name))
            {
                throw new PersistenceDataException(
                    $"No se puede actualizar RoleRecord '{record.Id}': el Role contiene el permiso duplicado '{name}'.");
            }
        }

        // ---------- FASE B: mutar ----------

        record.Name = role.Name;
        record.IsActive = role.IsActive;

        record.Permissions.RemoveAll(p => !desiredPermissionSet.Contains(p.Permission));

        var existingPermissions = record.Permissions.Select(p => p.Permission).ToHashSet(StringComparer.Ordinal);

        foreach (var permission in desiredPermissionNames.OrderBy(p => p, StringComparer.Ordinal))
        {
            if (existingPermissions.Contains(permission))
            {
                continue;
            }

            record.Permissions.Add(new RolePermissionRecord
            {
                RoleId = record.Id,
                Permission = permission,
                Role = record,
            });
        }
    }

    private static List<Permission> ParsePermissions(RoleRecord record)
    {
        var seen = new HashSet<Permission>();
        var permissions = new List<Permission>();

        foreach (var permissionRecord in record.Permissions.OrderBy(p => p.Permission, StringComparer.Ordinal))
        {
            if (permissionRecord.RoleId != record.Id)
            {
                throw new DomainValidationException(
                    $"Permission con RoleId '{permissionRecord.RoleId}' no coincide con Role '{record.Id}'.");
            }

            if (!Enum.TryParse<Permission>(permissionRecord.Permission, out var permission)
                || !Enum.IsDefined(permission))
            {
                throw new DomainValidationException(
                    $"Permission '{permissionRecord.Permission}' no es un valor válido.");
            }

            if (!seen.Add(permission))
            {
                throw new DomainValidationException(
                    $"Permission '{permissionRecord.Permission}' está duplicado.");
            }

            permissions.Add(permission);
        }

        return permissions;
    }
}
