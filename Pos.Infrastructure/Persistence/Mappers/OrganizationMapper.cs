using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Organizations;
using Pos.Infrastructure.Persistence.Exceptions;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Persistence.Mappers;

internal static class OrganizationMapper
{
    internal static Organization ToDomain(OrganizationRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        try
        {
            return Organization.Rehydrate(
                new OrganizationId(record.Id),
                record.Name,
                record.IsActive,
                record.CreatedAtUtc);
        }
        catch (DomainValidationException ex)
        {
            throw new PersistenceDataException(
                $"OrganizationRecord con Id '{record.Id}' contiene datos inválidos: {ex.Message}", ex);
        }
    }

    internal static OrganizationRecord ToRecord(Organization organization)
    {
        ArgumentNullException.ThrowIfNull(organization);

        return new OrganizationRecord
        {
            Id = organization.Id.Value,
            Name = organization.Name,
            IsActive = organization.IsActive,
            CreatedAtUtc = organization.CreatedAtUtc,
        };
    }

    internal static void UpdateRecord(Organization organization, OrganizationRecord record)
    {
        ArgumentNullException.ThrowIfNull(organization);
        ArgumentNullException.ThrowIfNull(record);

        if (record.Id != organization.Id.Value)
        {
            throw new PersistenceDataException(
                $"No se puede actualizar OrganizationRecord '{record.Id}': Id no coincide con Organization '{organization.Id}'.");
        }

        if (record.CreatedAtUtc != organization.CreatedAtUtc)
        {
            throw new PersistenceDataException(
                $"No se puede actualizar OrganizationRecord '{record.Id}': CreatedAtUtc no coincide.");
        }

        record.Name = organization.Name;
        record.IsActive = organization.IsActive;
    }
}
