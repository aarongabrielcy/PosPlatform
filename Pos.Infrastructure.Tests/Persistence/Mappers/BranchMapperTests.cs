using Pos.Infrastructure.Persistence.Exceptions;
using Pos.Infrastructure.Persistence.Mappers;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Tests.Persistence.Mappers;

public class BranchMapperTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    private static BranchRecord CreateValidRecord() => new()
    {
        Id = Guid.NewGuid(),
        OrganizationId = Guid.NewGuid(),
        Name = "Sucursal Centro",
        Code = "SUC-1",
        IsActive = true,
        CreatedAtUtc = CreatedAtUtc,
    };

    // ---------- ToDomain ----------

    [Fact]
    public void ToDomainReconstructsIdentifiers()
    {
        var record = CreateValidRecord();

        var branch = BranchMapper.ToDomain(record);

        Assert.Equal(record.Id, branch.Id.Value);
        Assert.Equal(record.OrganizationId, branch.OrganizationId.Value);
    }

    [Fact]
    public void ToDomainKeepsNameAndCode()
    {
        var record = CreateValidRecord();

        var branch = BranchMapper.ToDomain(record);

        Assert.Equal(record.Name, branch.Name);
        Assert.Equal(record.Code, branch.Code);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ToDomainKeepsIsActive(bool isActive)
    {
        var record = CreateValidRecord();
        record.IsActive = isActive;

        var branch = BranchMapper.ToDomain(record);

        Assert.Equal(isActive, branch.IsActive);
    }

    [Fact]
    public void ToDomainKeepsCreatedAtUtc()
    {
        var record = CreateValidRecord();

        var branch = BranchMapper.ToDomain(record);

        Assert.Equal(CreatedAtUtc, branch.CreatedAtUtc);
        Assert.Equal(TimeSpan.Zero, branch.CreatedAtUtc.Offset);
    }

    [Fact]
    public void ToDomainRejectsNullRecord()
    {
        Assert.Throws<ArgumentNullException>(() => BranchMapper.ToDomain(null!));
    }

    [Fact]
    public void ToDomainWrapsEmptyIdInPersistenceDataException()
    {
        var record = CreateValidRecord();
        record.Id = Guid.Empty;

        Assert.Throws<PersistenceDataException>(() => BranchMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsEmptyOrganizationIdInPersistenceDataException()
    {
        var record = CreateValidRecord();
        record.OrganizationId = Guid.Empty;

        Assert.Throws<PersistenceDataException>(() => BranchMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsInvalidCodeInPersistenceDataException()
    {
        var record = CreateValidRecord();
        record.Code = "SUC 1";

        Assert.Throws<PersistenceDataException>(() => BranchMapper.ToDomain(record));
    }

    // ---------- ToRecord ----------

    [Fact]
    public void ToRecordKeepsAllFields()
    {
        var branch = BranchMapper.ToDomain(CreateValidRecord());

        var record = BranchMapper.ToRecord(branch);

        Assert.Equal(branch.Id.Value, record.Id);
        Assert.Equal(branch.OrganizationId.Value, record.OrganizationId);
        Assert.Equal(branch.Name, record.Name);
        Assert.Equal(branch.Code, record.Code);
        Assert.Equal(branch.IsActive, record.IsActive);
        Assert.Equal(branch.CreatedAtUtc, record.CreatedAtUtc);
    }

    [Fact]
    public void ToRecordRejectsNullBranch()
    {
        Assert.Throws<ArgumentNullException>(() => BranchMapper.ToRecord(null!));
    }

    // ---------- UpdateRecord ----------

    [Fact]
    public void UpdateRecordUpdatesNameAndCode()
    {
        var record = CreateValidRecord();
        var branch = BranchMapper.ToDomain(record);
        branch.Rename("Sucursal Norte");
        branch.ChangeCode("SUC-2");

        BranchMapper.UpdateRecord(branch, record);

        Assert.Equal("Sucursal Norte", record.Name);
        Assert.Equal("SUC-2", record.Code);
    }

    [Fact]
    public void UpdateRecordUpdatesIsActive()
    {
        var record = CreateValidRecord();
        var branch = BranchMapper.ToDomain(record);
        branch.Deactivate();

        BranchMapper.UpdateRecord(branch, record);

        Assert.False(record.IsActive);
    }

    [Fact]
    public void UpdateRecordDoesNotChangeId()
    {
        var record = CreateValidRecord();
        var originalId = record.Id;
        var branch = BranchMapper.ToDomain(record);

        BranchMapper.UpdateRecord(branch, record);

        Assert.Equal(originalId, record.Id);
    }

    [Fact]
    public void UpdateRecordDoesNotChangeOrganizationId()
    {
        var record = CreateValidRecord();
        var originalOrganizationId = record.OrganizationId;
        var branch = BranchMapper.ToDomain(record);

        BranchMapper.UpdateRecord(branch, record);

        Assert.Equal(originalOrganizationId, record.OrganizationId);
    }

    [Fact]
    public void UpdateRecordDoesNotChangeCreatedAtUtc()
    {
        var record = CreateValidRecord();
        var branch = BranchMapper.ToDomain(record);

        BranchMapper.UpdateRecord(branch, record);

        Assert.Equal(CreatedAtUtc, record.CreatedAtUtc);
    }

    [Fact]
    public void UpdateRecordRejectsMismatchedId()
    {
        var record = CreateValidRecord();
        var branch = BranchMapper.ToDomain(record);
        record.Id = Guid.NewGuid();

        Assert.Throws<PersistenceDataException>(() => BranchMapper.UpdateRecord(branch, record));
    }

    [Fact]
    public void UpdateRecordRejectsMismatchedOrganizationId()
    {
        var record = CreateValidRecord();
        var branch = BranchMapper.ToDomain(record);
        record.OrganizationId = Guid.NewGuid();

        Assert.Throws<PersistenceDataException>(() => BranchMapper.UpdateRecord(branch, record));
    }

    [Fact]
    public void UpdateRecordRejectsMismatchedCreatedAtUtc()
    {
        var record = CreateValidRecord();
        var branch = BranchMapper.ToDomain(record);
        record.CreatedAtUtc = CreatedAtUtc.AddDays(1);

        Assert.Throws<PersistenceDataException>(() => BranchMapper.UpdateRecord(branch, record));
    }

    [Fact]
    public void UpdateRecordRejectsNullBranch()
    {
        var record = CreateValidRecord();

        Assert.Throws<ArgumentNullException>(() => BranchMapper.UpdateRecord(null!, record));
    }

    [Fact]
    public void UpdateRecordRejectsNullRecord()
    {
        var branch = BranchMapper.ToDomain(CreateValidRecord());

        Assert.Throws<ArgumentNullException>(() => BranchMapper.UpdateRecord(branch, null!));
    }
}
