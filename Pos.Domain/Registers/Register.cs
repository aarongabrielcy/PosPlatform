using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;

namespace Pos.Domain.Registers;

public sealed class Register
{
    public RegisterId Id { get; }

    public BranchId BranchId { get; }

    public string Name { get; private set; }

    public string Code { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public Register(RegisterId id, BranchId branchId, string name, string code, DateTimeOffset createdAtUtc)
        : this(id, branchId, name, code, true, createdAtUtc)
    {
    }

    private Register(
        RegisterId id,
        BranchId branchId,
        string name,
        string code,
        bool isActive,
        DateTimeOffset createdAtUtc)
    {
        Id = EnsureNotEmpty(id);
        BranchId = EnsureNotEmpty(branchId);
        Name = NormalizeName(name);
        Code = NormalizeCode(code);
        CreatedAtUtc = EnsureUtc(createdAtUtc);
        IsActive = isActive;
    }

    // Reconstruye estado ya persistido, incluyendo IsActive, sin pasar por Activate/Deactivate.
    public static Register Rehydrate(
        RegisterId id,
        BranchId branchId,
        string name,
        string code,
        bool isActive,
        DateTimeOffset createdAtUtc) =>
        new(id, branchId, name, code, isActive, createdAtUtc);

    public void Rename(string name)
    {
        Name = NormalizeName(name);
    }

    public void ChangeCode(string code)
    {
        Code = NormalizeCode(code);
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    private static RegisterId EnsureNotEmpty(RegisterId id)
    {
        if (id.Value == Guid.Empty)
        {
            throw new DomainValidationException("Id no puede ser vacío.");
        }

        return id;
    }

    private static BranchId EnsureNotEmpty(BranchId branchId)
    {
        if (branchId.Value == Guid.Empty)
        {
            throw new DomainValidationException("BranchId no puede ser vacío.");
        }

        return branchId;
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

    private static string NormalizeCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainValidationException("Code es obligatorio.");
        }

        var normalized = code.Trim().ToUpperInvariant();

        if (normalized.Length is < 2 or > 20)
        {
            throw new DomainValidationException("Code debe tener entre 2 y 20 caracteres.");
        }

        if (!normalized.All(c => c is (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '-'))
        {
            throw new DomainValidationException("Code solo puede contener letras A-Z, números 0-9 y guion medio.");
        }

        return normalized;
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
