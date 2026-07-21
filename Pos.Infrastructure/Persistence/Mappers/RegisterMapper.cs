using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Registers;
using Pos.Infrastructure.Persistence.Exceptions;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Persistence.Mappers;

internal static class RegisterMapper
{
    internal static Register ToDomain(RegisterRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        try
        {
            return Register.Rehydrate(
                new RegisterId(record.Id),
                new BranchId(record.BranchId),
                record.Name,
                record.Code,
                record.IsActive,
                record.CreatedAtUtc);
        }
        catch (DomainValidationException ex)
        {
            throw new PersistenceDataException(
                $"RegisterRecord con Id '{record.Id}' contiene datos inválidos: {ex.Message}", ex);
        }
    }

    internal static RegisterRecord ToRecord(Register register)
    {
        ArgumentNullException.ThrowIfNull(register);

        return new RegisterRecord
        {
            Id = register.Id.Value,
            BranchId = register.BranchId.Value,
            Name = register.Name,
            Code = register.Code,
            IsActive = register.IsActive,
            CreatedAtUtc = register.CreatedAtUtc,
        };
    }

    internal static void UpdateRecord(Register register, RegisterRecord record)
    {
        ArgumentNullException.ThrowIfNull(register);
        ArgumentNullException.ThrowIfNull(record);

        if (record.Id != register.Id.Value)
        {
            throw new PersistenceDataException(
                $"No se puede actualizar RegisterRecord '{record.Id}': Id no coincide con Register '{register.Id}'.");
        }

        if (record.BranchId != register.BranchId.Value)
        {
            throw new PersistenceDataException(
                $"No se puede actualizar RegisterRecord '{record.Id}': BranchId no coincide.");
        }

        if (record.CreatedAtUtc != register.CreatedAtUtc)
        {
            throw new PersistenceDataException(
                $"No se puede actualizar RegisterRecord '{record.Id}': CreatedAtUtc no coincide.");
        }

        record.Name = register.Name;
        record.Code = register.Code;
        record.IsActive = register.IsActive;
    }
}
