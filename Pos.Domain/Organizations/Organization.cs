using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;

namespace Pos.Domain.Organizations;

public sealed class Organization
{
    public OrganizationId Id { get; }

    public string Name { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public Organization(OrganizationId id, string name, DateTimeOffset createdAtUtc)
        : this(id, name, true, createdAtUtc)
    {
    }

    private Organization(OrganizationId id, string name, bool isActive, DateTimeOffset createdAtUtc)
    {
        Id = EnsureNotEmpty(id);
        Name = NormalizeName(name);
        CreatedAtUtc = EnsureUtc(createdAtUtc);
        IsActive = isActive;
    }

    // Reconstruye estado ya persistido, incluyendo IsActive, sin pasar por Activate/Deactivate.
    public static Organization Rehydrate(OrganizationId id, string name, bool isActive, DateTimeOffset createdAtUtc) =>
        new(id, name, isActive, createdAtUtc);

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

    private static OrganizationId EnsureNotEmpty(OrganizationId id)
    {
        if (id.Value == Guid.Empty)
        {
            throw new DomainValidationException("Id no puede ser vacío.");
        }

        return id;
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("Name es obligatorio.");
        }

        var trimmed = name.Trim();

        if (trimmed.Length is < 2 or > 120)
        {
            throw new DomainValidationException("Name debe tener entre 2 y 120 caracteres.");
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
