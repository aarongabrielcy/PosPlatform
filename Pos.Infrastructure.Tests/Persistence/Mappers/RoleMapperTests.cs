using Pos.Domain.Security;
using Pos.Infrastructure.Persistence.Exceptions;
using Pos.Infrastructure.Persistence.Mappers;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Tests.Persistence.Mappers;

public class RoleMapperTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    private static RoleRecord CreateValidRecord(Guid? roleId = null)
    {
        var id = roleId ?? Guid.NewGuid();

        var record = new RoleRecord
        {
            Id = id,
            OrganizationId = Guid.NewGuid(),
            Name = "Cajero",
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
        };

        record.Permissions.Add(new RolePermissionRecord
        {
            RoleId = id,
            Permission = Permission.ProcessSale.ToString(),
            Role = record,
        });

        return record;
    }

    // ---------- ToDomain ----------

    [Fact]
    public void ToDomainReconstructsIdentifiers()
    {
        var record = CreateValidRecord();

        var role = RoleMapper.ToDomain(record);

        Assert.Equal(record.Id, role.Id.Value);
        Assert.Equal(record.OrganizationId, role.OrganizationId.Value);
    }

    [Fact]
    public void ToDomainKeepsName()
    {
        var record = CreateValidRecord();

        var role = RoleMapper.ToDomain(record);

        Assert.Equal(record.Name, role.Name);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ToDomainKeepsIsActive(bool isActive)
    {
        var record = CreateValidRecord();
        record.IsActive = isActive;

        var role = RoleMapper.ToDomain(record);

        Assert.Equal(isActive, role.IsActive);
    }

    [Fact]
    public void ToDomainKeepsCreatedAtUtc()
    {
        var record = CreateValidRecord();

        var role = RoleMapper.ToDomain(record);

        Assert.Equal(CreatedAtUtc, role.CreatedAtUtc);
        Assert.Equal(TimeSpan.Zero, role.CreatedAtUtc.Offset);
    }

    [Fact]
    public void ToDomainReconstructsPermissions()
    {
        var id = Guid.NewGuid();
        var record = CreateValidRecord(id);
        record.Permissions.Add(new RolePermissionRecord
        {
            RoleId = id,
            Permission = Permission.ApplyDiscount.ToString(),
            Role = record,
        });

        var role = RoleMapper.ToDomain(record);

        Assert.Equal(2, role.Permissions.Count);
        Assert.True(role.HasPermission(Permission.ProcessSale));
        Assert.True(role.HasPermission(Permission.ApplyDiscount));
    }

    [Fact]
    public void ToDomainWithNoPermissionsReturnsEmptyCollection()
    {
        var record = CreateValidRecord();
        record.Permissions.Clear();

        var role = RoleMapper.ToDomain(record);

        Assert.Empty(role.Permissions);
    }

    [Fact]
    public void ToDomainRejectsNullRecord()
    {
        Assert.Throws<ArgumentNullException>(() => RoleMapper.ToDomain(null!));
    }

    [Fact]
    public void ToDomainWrapsEmptyIdInPersistenceDataException()
    {
        var record = CreateValidRecord();
        record.Id = Guid.Empty;

        Assert.Throws<PersistenceDataException>(() => RoleMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsEmptyOrganizationIdInPersistenceDataException()
    {
        var record = CreateValidRecord();
        record.OrganizationId = Guid.Empty;

        Assert.Throws<PersistenceDataException>(() => RoleMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsInvalidNameInPersistenceDataException()
    {
        var record = CreateValidRecord();
        record.Name = "A";

        Assert.Throws<PersistenceDataException>(() => RoleMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsUnknownPermissionInPersistenceDataException()
    {
        var id = Guid.NewGuid();
        var record = CreateValidRecord(id);
        record.Permissions.Clear();
        record.Permissions.Add(new RolePermissionRecord { RoleId = id, Permission = "NotARealPermission", Role = record });

        Assert.Throws<PersistenceDataException>(() => RoleMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsDuplicatePermissionsInPersistenceDataException()
    {
        var id = Guid.NewGuid();
        var record = CreateValidRecord(id);
        record.Permissions.Add(new RolePermissionRecord
        {
            RoleId = id,
            Permission = Permission.ProcessSale.ToString(),
            Role = record,
        });

        Assert.Throws<PersistenceDataException>(() => RoleMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainRejectsPermissionWithDifferentRoleId()
    {
        var id = Guid.NewGuid();
        var record = CreateValidRecord(id);
        record.Permissions.Add(new RolePermissionRecord
        {
            RoleId = Guid.NewGuid(),
            Permission = Permission.ApplyDiscount.ToString(),
            Role = record,
        });

        Assert.Throws<PersistenceDataException>(() => RoleMapper.ToDomain(record));
    }

    // ---------- ToRecord ----------

    [Fact]
    public void ToRecordKeepsAllFields()
    {
        var role = RoleMapper.ToDomain(CreateValidRecord());

        var record = RoleMapper.ToRecord(role);

        Assert.Equal(role.Id.Value, record.Id);
        Assert.Equal(role.OrganizationId.Value, record.OrganizationId);
        Assert.Equal(role.Name, record.Name);
        Assert.Equal(role.IsActive, record.IsActive);
        Assert.Equal(role.CreatedAtUtc, record.CreatedAtUtc);
    }

    [Fact]
    public void ToRecordCreatesChildPermissionRecords()
    {
        var role = RoleMapper.ToDomain(CreateValidRecord());

        var record = RoleMapper.ToRecord(role);

        var permission = Assert.Single(record.Permissions);
        Assert.Equal(record.Id, permission.RoleId);
        Assert.Equal(Permission.ProcessSale.ToString(), permission.Permission);
    }

    [Fact]
    public void ToRecordCopiesPermissionsIntoIndependentCollection()
    {
        var role = RoleMapper.ToDomain(CreateValidRecord());

        var firstRecord = RoleMapper.ToRecord(role);
        var secondRecord = RoleMapper.ToRecord(role);

        Assert.NotSame(firstRecord.Permissions, secondRecord.Permissions);
    }

    [Fact]
    public void ToRecordRejectsNullRole()
    {
        Assert.Throws<ArgumentNullException>(() => RoleMapper.ToRecord(null!));
    }

    // ---------- UpdateRecord ----------

    [Fact]
    public void UpdateRecordUpdatesName()
    {
        var record = CreateValidRecord();
        var role = RoleMapper.ToDomain(record);
        role.Rename("Gerente");

        RoleMapper.UpdateRecord(role, record);

        Assert.Equal("Gerente", record.Name);
    }

    [Fact]
    public void UpdateRecordUpdatesIsActive()
    {
        var record = CreateValidRecord();
        var role = RoleMapper.ToDomain(record);
        role.Deactivate();

        RoleMapper.UpdateRecord(role, record);

        Assert.False(record.IsActive);
    }

    [Fact]
    public void UpdateRecordAddsGrantedPermission()
    {
        var record = CreateValidRecord();
        var role = RoleMapper.ToDomain(record);
        role.GrantPermission(Permission.ManageProducts);

        RoleMapper.UpdateRecord(role, record);

        Assert.Contains(record.Permissions, p => p.Permission == Permission.ManageProducts.ToString());
        Assert.Equal(2, record.Permissions.Count);
    }

    [Fact]
    public void UpdateRecordRemovesRevokedPermission()
    {
        var record = CreateValidRecord();
        var role = RoleMapper.ToDomain(record);
        role.RevokePermission(Permission.ProcessSale);

        RoleMapper.UpdateRecord(role, record);

        Assert.Empty(record.Permissions);
    }

    [Fact]
    public void UpdateRecordDoesNotChangeId()
    {
        var record = CreateValidRecord();
        var originalId = record.Id;
        var role = RoleMapper.ToDomain(record);

        RoleMapper.UpdateRecord(role, record);

        Assert.Equal(originalId, record.Id);
    }

    [Fact]
    public void UpdateRecordDoesNotChangeOrganizationId()
    {
        var record = CreateValidRecord();
        var originalOrganizationId = record.OrganizationId;
        var role = RoleMapper.ToDomain(record);

        RoleMapper.UpdateRecord(role, record);

        Assert.Equal(originalOrganizationId, record.OrganizationId);
    }

    [Fact]
    public void UpdateRecordDoesNotChangeCreatedAtUtc()
    {
        var record = CreateValidRecord();
        var role = RoleMapper.ToDomain(record);

        RoleMapper.UpdateRecord(role, record);

        Assert.Equal(CreatedAtUtc, record.CreatedAtUtc);
    }

    [Fact]
    public void UpdateRecordRejectsMismatchedId()
    {
        var record = CreateValidRecord();
        var role = RoleMapper.ToDomain(record);
        record.Id = Guid.NewGuid();

        Assert.Throws<PersistenceDataException>(() => RoleMapper.UpdateRecord(role, record));
    }

    [Fact]
    public void UpdateRecordRejectsMismatchedOrganizationId()
    {
        var record = CreateValidRecord();
        var role = RoleMapper.ToDomain(record);
        record.OrganizationId = Guid.NewGuid();

        Assert.Throws<PersistenceDataException>(() => RoleMapper.UpdateRecord(role, record));
    }

    [Fact]
    public void UpdateRecordRejectsMismatchedCreatedAtUtc()
    {
        var record = CreateValidRecord();
        var role = RoleMapper.ToDomain(record);
        record.CreatedAtUtc = CreatedAtUtc.AddDays(1);

        Assert.Throws<PersistenceDataException>(() => RoleMapper.UpdateRecord(role, record));
    }

    [Fact]
    public void UpdateRecordRejectsNullRole()
    {
        var record = CreateValidRecord();

        Assert.Throws<ArgumentNullException>(() => RoleMapper.UpdateRecord(null!, record));
    }

    [Fact]
    public void UpdateRecordRejectsNullRecord()
    {
        var role = RoleMapper.ToDomain(CreateValidRecord());

        Assert.Throws<ArgumentNullException>(() => RoleMapper.UpdateRecord(role, null!));
    }

    // ---------- UpdateRecord: validación de permisos en dos fases ----------

    [Fact]
    public void UpdateRecordRejectsUnknownPersistedPermission()
    {
        var record = CreateValidRecord();
        var role = RoleMapper.ToDomain(record);
        record.Permissions.Add(new RolePermissionRecord
        {
            RoleId = record.Id,
            Permission = "NotARealPermission",
            Role = record,
        });

        Assert.Throws<PersistenceDataException>(() => RoleMapper.UpdateRecord(role, record));
    }

    [Fact]
    public void UpdateRecordRejectsDuplicatePersistedPermission()
    {
        var record = CreateValidRecord();
        var role = RoleMapper.ToDomain(record);
        record.Permissions.Add(new RolePermissionRecord
        {
            RoleId = record.Id,
            Permission = Permission.ProcessSale.ToString(),
            Role = record,
        });

        Assert.Throws<PersistenceDataException>(() => RoleMapper.UpdateRecord(role, record));
    }

    [Fact]
    public void UpdateRecordRejectsPersistedPermissionWithDifferentRoleId()
    {
        var record = CreateValidRecord();
        var role = RoleMapper.ToDomain(record);
        record.Permissions.Add(new RolePermissionRecord
        {
            RoleId = Guid.NewGuid(),
            Permission = Permission.ApplyDiscount.ToString(),
            Role = record,
        });

        Assert.Throws<PersistenceDataException>(() => RoleMapper.UpdateRecord(role, record));
    }

    [Fact]
    public void UpdateRecordFailureDueToUnknownPermissionLeavesNameAndIsActiveUnchanged()
    {
        var record = CreateValidRecord();
        var role = RoleMapper.ToDomain(record);
        role.Rename("Gerente");
        role.Deactivate();

        var originalName = record.Name;
        var originalIsActive = record.IsActive;

        record.Permissions.Add(new RolePermissionRecord
        {
            RoleId = record.Id,
            Permission = "NotARealPermission",
            Role = record,
        });

        Assert.Throws<PersistenceDataException>(() => RoleMapper.UpdateRecord(role, record));

        Assert.Equal(originalName, record.Name);
        Assert.Equal(originalIsActive, record.IsActive);
    }

    [Fact]
    public void UpdateRecordFailureDueToUnknownPermissionLeavesPermissionCollectionUnchanged()
    {
        var record = CreateValidRecord();
        var role = RoleMapper.ToDomain(record);
        role.GrantPermission(Permission.ManageProducts);
        role.RevokePermission(Permission.ProcessSale);

        record.Permissions.Add(new RolePermissionRecord
        {
            RoleId = record.Id,
            Permission = "NotARealPermission",
            Role = record,
        });

        var originalPermissions = record.Permissions.Select(p => p.Permission).OrderBy(p => p, StringComparer.Ordinal).ToList();

        Assert.Throws<PersistenceDataException>(() => RoleMapper.UpdateRecord(role, record));

        var permissionsAfterFailure = record.Permissions.Select(p => p.Permission).OrderBy(p => p, StringComparer.Ordinal).ToList();
        Assert.Equal(originalPermissions, permissionsAfterFailure);
    }

    [Fact]
    public void UpdateRecordWithNewNameFailsAtomicallyWhenRecordHasCorruptPermissionAndLeavesOriginalNameIntact()
    {
        var record = CreateValidRecord();
        var role = RoleMapper.ToDomain(record);
        var originalName = record.Name;
        role.Rename("Nuevo Nombre");

        record.Permissions.Add(new RolePermissionRecord
        {
            RoleId = Guid.NewGuid(),
            Permission = Permission.ApplyDiscount.ToString(),
            Role = record,
        });

        Assert.Throws<PersistenceDataException>(() => RoleMapper.UpdateRecord(role, record));

        Assert.Equal(originalName, record.Name);
        Assert.NotEqual("Nuevo Nombre", record.Name);
    }

    [Fact]
    public void UpdateRecordValidSynchronizationAddsAndRemovesPermissions()
    {
        var id = Guid.NewGuid();
        var record = CreateValidRecord(id);
        record.Permissions.Add(new RolePermissionRecord
        {
            RoleId = id,
            Permission = Permission.ApplyDiscount.ToString(),
            Role = record,
        });
        var role = RoleMapper.ToDomain(record);

        role.RevokePermission(Permission.ProcessSale);
        role.GrantPermission(Permission.ManageProducts);

        RoleMapper.UpdateRecord(role, record);

        var permissions = record.Permissions.Select(p => p.Permission).ToList();
        Assert.Equal(2, permissions.Count);
        Assert.Contains(Permission.ApplyDiscount.ToString(), permissions);
        Assert.Contains(Permission.ManageProducts.ToString(), permissions);
        Assert.DoesNotContain(Permission.ProcessSale.ToString(), permissions);
    }
}
