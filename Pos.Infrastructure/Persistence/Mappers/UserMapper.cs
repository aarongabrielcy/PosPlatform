using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Users;
using Pos.Infrastructure.Persistence.Exceptions;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Persistence.Mappers;

internal static class UserMapper
{
    internal static User ToDomain(UserRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        try
        {
            return User.Rehydrate(
                new UserId(record.Id),
                new OrganizationId(record.OrganizationId),
                new RoleId(record.RoleId),
                record.Username,
                record.DisplayName,
                record.IsActive,
                record.CreatedAtUtc);
        }
        catch (DomainValidationException ex)
        {
            throw new PersistenceDataException(
                $"UserRecord con Id '{record.Id}' contiene datos inválidos: {ex.Message}", ex);
        }
    }

    internal static UserRecord ToRecord(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new UserRecord
        {
            Id = user.Id.Value,
            OrganizationId = user.OrganizationId.Value,
            RoleId = user.RoleId.Value,
            Username = user.Username,
            DisplayName = user.DisplayName,
            IsActive = user.IsActive,
            CreatedAtUtc = user.CreatedAtUtc,
        };
    }

    // OrganizationId es inmutable en Domain (sin mutador). RoleId sí es mutable vía
    // User.ChangeRole, por lo que se sincroniza en lugar de protegerse.
    internal static void UpdateRecord(User user, UserRecord record)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(record);

        if (record.Id != user.Id.Value)
        {
            throw new PersistenceDataException(
                $"No se puede actualizar UserRecord '{record.Id}': Id no coincide con User '{user.Id}'.");
        }

        if (record.OrganizationId != user.OrganizationId.Value)
        {
            throw new PersistenceDataException(
                $"No se puede actualizar UserRecord '{record.Id}': OrganizationId no coincide.");
        }

        if (record.CreatedAtUtc != user.CreatedAtUtc)
        {
            throw new PersistenceDataException(
                $"No se puede actualizar UserRecord '{record.Id}': CreatedAtUtc no coincide.");
        }

        record.RoleId = user.RoleId.Value;
        record.Username = user.Username;
        record.DisplayName = user.DisplayName;
        record.IsActive = user.IsActive;
    }
}
