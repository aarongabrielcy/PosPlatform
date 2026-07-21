using Pos.Infrastructure.Persistence.Exceptions;
using Pos.Infrastructure.Persistence.Mappers;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Tests.Persistence.Mappers;

public class UserMapperTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    private static UserRecord CreateValidRecord() => new()
    {
        Id = Guid.NewGuid(),
        OrganizationId = Guid.NewGuid(),
        RoleId = Guid.NewGuid(),
        Username = "JPEREZ",
        DisplayName = "Juan Pérez",
        IsActive = true,
        CreatedAtUtc = CreatedAtUtc,
    };

    // ---------- ToDomain ----------

    [Fact]
    public void ToDomainReconstructsIdentifiers()
    {
        var record = CreateValidRecord();

        var user = UserMapper.ToDomain(record);

        Assert.Equal(record.Id, user.Id.Value);
        Assert.Equal(record.OrganizationId, user.OrganizationId.Value);
        Assert.Equal(record.RoleId, user.RoleId.Value);
    }

    [Fact]
    public void ToDomainKeepsUsernameAndDisplayName()
    {
        var record = CreateValidRecord();

        var user = UserMapper.ToDomain(record);

        Assert.Equal(record.Username, user.Username);
        Assert.Equal(record.DisplayName, user.DisplayName);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ToDomainKeepsIsActive(bool isActive)
    {
        var record = CreateValidRecord();
        record.IsActive = isActive;

        var user = UserMapper.ToDomain(record);

        Assert.Equal(isActive, user.IsActive);
    }

    [Fact]
    public void ToDomainKeepsCreatedAtUtc()
    {
        var record = CreateValidRecord();

        var user = UserMapper.ToDomain(record);

        Assert.Equal(CreatedAtUtc, user.CreatedAtUtc);
        Assert.Equal(TimeSpan.Zero, user.CreatedAtUtc.Offset);
    }

    [Fact]
    public void ToDomainRejectsNullRecord()
    {
        Assert.Throws<ArgumentNullException>(() => UserMapper.ToDomain(null!));
    }

    [Fact]
    public void ToDomainWrapsEmptyIdInPersistenceDataException()
    {
        var record = CreateValidRecord();
        record.Id = Guid.Empty;

        Assert.Throws<PersistenceDataException>(() => UserMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsEmptyOrganizationIdInPersistenceDataException()
    {
        var record = CreateValidRecord();
        record.OrganizationId = Guid.Empty;

        Assert.Throws<PersistenceDataException>(() => UserMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsEmptyRoleIdInPersistenceDataException()
    {
        var record = CreateValidRecord();
        record.RoleId = Guid.Empty;

        Assert.Throws<PersistenceDataException>(() => UserMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsInvalidUsernameInPersistenceDataException()
    {
        var record = CreateValidRecord();
        record.Username = "a";

        Assert.Throws<PersistenceDataException>(() => UserMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsInvalidDisplayNameInPersistenceDataException()
    {
        var record = CreateValidRecord();
        record.DisplayName = "A";

        Assert.Throws<PersistenceDataException>(() => UserMapper.ToDomain(record));
    }

    // ---------- ToRecord ----------

    [Fact]
    public void ToRecordKeepsAllFields()
    {
        var user = UserMapper.ToDomain(CreateValidRecord());

        var record = UserMapper.ToRecord(user);

        Assert.Equal(user.Id.Value, record.Id);
        Assert.Equal(user.OrganizationId.Value, record.OrganizationId);
        Assert.Equal(user.RoleId.Value, record.RoleId);
        Assert.Equal(user.Username, record.Username);
        Assert.Equal(user.DisplayName, record.DisplayName);
        Assert.Equal(user.IsActive, record.IsActive);
        Assert.Equal(user.CreatedAtUtc, record.CreatedAtUtc);
    }

    [Fact]
    public void ToRecordRejectsNullUser()
    {
        Assert.Throws<ArgumentNullException>(() => UserMapper.ToRecord(null!));
    }

    // ---------- UpdateRecord ----------

    [Fact]
    public void UpdateRecordUpdatesDisplayName()
    {
        var record = CreateValidRecord();
        var user = UserMapper.ToDomain(record);
        user.ChangeDisplayName("Maria Lopez");

        UserMapper.UpdateRecord(user, record);

        Assert.Equal("Maria Lopez", record.DisplayName);
    }

    [Fact]
    public void UpdateRecordUpdatesRoleId()
    {
        var record = CreateValidRecord();
        var user = UserMapper.ToDomain(record);
        var newRoleId = Guid.NewGuid();
        user.ChangeRole(new Pos.Domain.Common.Identifiers.RoleId(newRoleId));

        UserMapper.UpdateRecord(user, record);

        Assert.Equal(newRoleId, record.RoleId);
    }

    [Fact]
    public void UpdateRecordUpdatesIsActive()
    {
        var record = CreateValidRecord();
        var user = UserMapper.ToDomain(record);
        user.Deactivate();

        UserMapper.UpdateRecord(user, record);

        Assert.False(record.IsActive);
    }

    [Fact]
    public void UpdateRecordDoesNotChangeId()
    {
        var record = CreateValidRecord();
        var originalId = record.Id;
        var user = UserMapper.ToDomain(record);

        UserMapper.UpdateRecord(user, record);

        Assert.Equal(originalId, record.Id);
    }

    [Fact]
    public void UpdateRecordDoesNotChangeOrganizationId()
    {
        var record = CreateValidRecord();
        var originalOrganizationId = record.OrganizationId;
        var user = UserMapper.ToDomain(record);

        UserMapper.UpdateRecord(user, record);

        Assert.Equal(originalOrganizationId, record.OrganizationId);
    }

    [Fact]
    public void UpdateRecordDoesNotChangeCreatedAtUtc()
    {
        var record = CreateValidRecord();
        var user = UserMapper.ToDomain(record);

        UserMapper.UpdateRecord(user, record);

        Assert.Equal(CreatedAtUtc, record.CreatedAtUtc);
    }

    [Fact]
    public void UpdateRecordRejectsMismatchedId()
    {
        var record = CreateValidRecord();
        var user = UserMapper.ToDomain(record);
        record.Id = Guid.NewGuid();

        Assert.Throws<PersistenceDataException>(() => UserMapper.UpdateRecord(user, record));
    }

    [Fact]
    public void UpdateRecordRejectsMismatchedOrganizationId()
    {
        var record = CreateValidRecord();
        var user = UserMapper.ToDomain(record);
        record.OrganizationId = Guid.NewGuid();

        Assert.Throws<PersistenceDataException>(() => UserMapper.UpdateRecord(user, record));
    }

    [Fact]
    public void UpdateRecordRejectsMismatchedCreatedAtUtc()
    {
        var record = CreateValidRecord();
        var user = UserMapper.ToDomain(record);
        record.CreatedAtUtc = CreatedAtUtc.AddDays(1);

        Assert.Throws<PersistenceDataException>(() => UserMapper.UpdateRecord(user, record));
    }

    [Fact]
    public void UpdateRecordRejectsNullUser()
    {
        var record = CreateValidRecord();

        Assert.Throws<ArgumentNullException>(() => UserMapper.UpdateRecord(null!, record));
    }

    [Fact]
    public void UpdateRecordRejectsNullRecord()
    {
        var user = UserMapper.ToDomain(CreateValidRecord());

        Assert.Throws<ArgumentNullException>(() => UserMapper.UpdateRecord(user, null!));
    }
}
