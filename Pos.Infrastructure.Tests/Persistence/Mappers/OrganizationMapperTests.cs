using Pos.Infrastructure.Persistence.Exceptions;
using Pos.Infrastructure.Persistence.Mappers;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Tests.Persistence.Mappers;

public class OrganizationMapperTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    private static OrganizationRecord CreateValidRecord() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Acme",
        IsActive = true,
        CreatedAtUtc = CreatedAtUtc,
    };

    // ---------- ToDomain ----------

    [Fact]
    public void ToDomainReconstructsIdentifier()
    {
        var record = CreateValidRecord();

        var organization = OrganizationMapper.ToDomain(record);

        Assert.Equal(record.Id, organization.Id.Value);
    }

    [Fact]
    public void ToDomainKeepsName()
    {
        var record = CreateValidRecord();

        var organization = OrganizationMapper.ToDomain(record);

        Assert.Equal(record.Name, organization.Name);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ToDomainKeepsIsActive(bool isActive)
    {
        var record = CreateValidRecord();
        record.IsActive = isActive;

        var organization = OrganizationMapper.ToDomain(record);

        Assert.Equal(isActive, organization.IsActive);
    }

    [Fact]
    public void ToDomainKeepsCreatedAtUtc()
    {
        var record = CreateValidRecord();

        var organization = OrganizationMapper.ToDomain(record);

        Assert.Equal(CreatedAtUtc, organization.CreatedAtUtc);
        Assert.Equal(TimeSpan.Zero, organization.CreatedAtUtc.Offset);
    }

    [Fact]
    public void ToDomainRejectsNullRecord()
    {
        Assert.Throws<ArgumentNullException>(() => OrganizationMapper.ToDomain(null!));
    }

    [Fact]
    public void ToDomainWrapsEmptyIdInPersistenceDataException()
    {
        var record = CreateValidRecord();
        record.Id = Guid.Empty;

        Assert.Throws<PersistenceDataException>(() => OrganizationMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsInvalidNameInPersistenceDataException()
    {
        var record = CreateValidRecord();
        record.Name = "A";

        Assert.Throws<PersistenceDataException>(() => OrganizationMapper.ToDomain(record));
    }

    // ---------- ToRecord ----------

    [Fact]
    public void ToRecordKeepsAllFields()
    {
        var organization = OrganizationMapper.ToDomain(CreateValidRecord());

        var record = OrganizationMapper.ToRecord(organization);

        Assert.Equal(organization.Id.Value, record.Id);
        Assert.Equal(organization.Name, record.Name);
        Assert.Equal(organization.IsActive, record.IsActive);
        Assert.Equal(organization.CreatedAtUtc, record.CreatedAtUtc);
    }

    [Fact]
    public void ToRecordRejectsNullOrganization()
    {
        Assert.Throws<ArgumentNullException>(() => OrganizationMapper.ToRecord(null!));
    }

    // ---------- UpdateRecord ----------

    [Fact]
    public void UpdateRecordUpdatesName()
    {
        var record = CreateValidRecord();
        var organization = OrganizationMapper.ToDomain(record);
        organization.Rename("Acme Corp");

        OrganizationMapper.UpdateRecord(organization, record);

        Assert.Equal("Acme Corp", record.Name);
    }

    [Fact]
    public void UpdateRecordUpdatesIsActive()
    {
        var record = CreateValidRecord();
        var organization = OrganizationMapper.ToDomain(record);
        organization.Deactivate();

        OrganizationMapper.UpdateRecord(organization, record);

        Assert.False(record.IsActive);
    }

    [Fact]
    public void UpdateRecordDoesNotChangeId()
    {
        var record = CreateValidRecord();
        var originalId = record.Id;
        var organization = OrganizationMapper.ToDomain(record);

        OrganizationMapper.UpdateRecord(organization, record);

        Assert.Equal(originalId, record.Id);
    }

    [Fact]
    public void UpdateRecordDoesNotChangeCreatedAtUtc()
    {
        var record = CreateValidRecord();
        var organization = OrganizationMapper.ToDomain(record);

        OrganizationMapper.UpdateRecord(organization, record);

        Assert.Equal(CreatedAtUtc, record.CreatedAtUtc);
    }

    [Fact]
    public void UpdateRecordRejectsMismatchedId()
    {
        var record = CreateValidRecord();
        var organization = OrganizationMapper.ToDomain(record);
        record.Id = Guid.NewGuid();

        Assert.Throws<PersistenceDataException>(() => OrganizationMapper.UpdateRecord(organization, record));
    }

    [Fact]
    public void UpdateRecordRejectsMismatchedCreatedAtUtc()
    {
        var record = CreateValidRecord();
        var organization = OrganizationMapper.ToDomain(record);
        record.CreatedAtUtc = CreatedAtUtc.AddDays(1);

        Assert.Throws<PersistenceDataException>(() => OrganizationMapper.UpdateRecord(organization, record));
    }

    [Fact]
    public void UpdateRecordRejectsNullOrganization()
    {
        var record = CreateValidRecord();

        Assert.Throws<ArgumentNullException>(() => OrganizationMapper.UpdateRecord(null!, record));
    }

    [Fact]
    public void UpdateRecordRejectsNullRecord()
    {
        var organization = OrganizationMapper.ToDomain(CreateValidRecord());

        Assert.Throws<ArgumentNullException>(() => OrganizationMapper.UpdateRecord(organization, null!));
    }
}
