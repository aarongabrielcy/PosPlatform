using Pos.Domain.Branches;
using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;
using Pos.Infrastructure.Persistence.Exceptions;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Persistence.Mappers;

internal static class BranchMapper
{
    internal static Branch ToDomain(BranchRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        try
        {
            return Branch.Rehydrate(
                new BranchId(record.Id),
                new OrganizationId(record.OrganizationId),
                record.Name,
                record.Code,
                record.IsActive,
                record.CreatedAtUtc);
        }
        catch (DomainValidationException ex)
        {
            throw new PersistenceDataException(
                $"BranchRecord con Id '{record.Id}' contiene datos inválidos: {ex.Message}", ex);
        }
    }

    internal static BranchRecord ToRecord(Branch branch)
    {
        ArgumentNullException.ThrowIfNull(branch);

        return new BranchRecord
        {
            Id = branch.Id.Value,
            OrganizationId = branch.OrganizationId.Value,
            Name = branch.Name,
            Code = branch.Code,
            IsActive = branch.IsActive,
            CreatedAtUtc = branch.CreatedAtUtc,
        };
    }

    internal static void UpdateRecord(Branch branch, BranchRecord record)
    {
        ArgumentNullException.ThrowIfNull(branch);
        ArgumentNullException.ThrowIfNull(record);

        if (record.Id != branch.Id.Value)
        {
            throw new PersistenceDataException(
                $"No se puede actualizar BranchRecord '{record.Id}': Id no coincide con Branch '{branch.Id}'.");
        }

        if (record.OrganizationId != branch.OrganizationId.Value)
        {
            throw new PersistenceDataException(
                $"No se puede actualizar BranchRecord '{record.Id}': OrganizationId no coincide.");
        }

        if (record.CreatedAtUtc != branch.CreatedAtUtc)
        {
            throw new PersistenceDataException(
                $"No se puede actualizar BranchRecord '{record.Id}': CreatedAtUtc no coincide.");
        }

        record.Name = branch.Name;
        record.Code = branch.Code;
        record.IsActive = branch.IsActive;
    }
}
