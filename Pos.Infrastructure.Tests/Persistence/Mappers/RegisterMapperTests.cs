using Pos.Infrastructure.Persistence.Exceptions;
using Pos.Infrastructure.Persistence.Mappers;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Tests.Persistence.Mappers;

public class RegisterMapperTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    private static RegisterRecord CreateValidRecord() => new()
    {
        Id = Guid.NewGuid(),
        BranchId = Guid.NewGuid(),
        Name = "Caja 1",
        Code = "CAJA-1",
        IsActive = true,
        CreatedAtUtc = CreatedAtUtc,
    };

    // ---------- ToDomain ----------

    [Fact]
    public void ToDomainReconstructsIdentifiers()
    {
        var record = CreateValidRecord();

        var register = RegisterMapper.ToDomain(record);

        Assert.Equal(record.Id, register.Id.Value);
        Assert.Equal(record.BranchId, register.BranchId.Value);
    }

    [Fact]
    public void ToDomainKeepsNameAndCode()
    {
        var record = CreateValidRecord();

        var register = RegisterMapper.ToDomain(record);

        Assert.Equal(record.Name, register.Name);
        Assert.Equal(record.Code, register.Code);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ToDomainKeepsIsActive(bool isActive)
    {
        var record = CreateValidRecord();
        record.IsActive = isActive;

        var register = RegisterMapper.ToDomain(record);

        Assert.Equal(isActive, register.IsActive);
    }

    [Fact]
    public void ToDomainKeepsCreatedAtUtc()
    {
        var record = CreateValidRecord();

        var register = RegisterMapper.ToDomain(record);

        Assert.Equal(CreatedAtUtc, register.CreatedAtUtc);
        Assert.Equal(TimeSpan.Zero, register.CreatedAtUtc.Offset);
    }

    [Fact]
    public void ToDomainRejectsNullRecord()
    {
        Assert.Throws<ArgumentNullException>(() => RegisterMapper.ToDomain(null!));
    }

    [Fact]
    public void ToDomainWrapsEmptyIdInPersistenceDataException()
    {
        var record = CreateValidRecord();
        record.Id = Guid.Empty;

        Assert.Throws<PersistenceDataException>(() => RegisterMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsEmptyBranchIdInPersistenceDataException()
    {
        var record = CreateValidRecord();
        record.BranchId = Guid.Empty;

        Assert.Throws<PersistenceDataException>(() => RegisterMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsInvalidCodeInPersistenceDataException()
    {
        var record = CreateValidRecord();
        record.Code = "CAJA 1";

        Assert.Throws<PersistenceDataException>(() => RegisterMapper.ToDomain(record));
    }

    // ---------- ToRecord ----------

    [Fact]
    public void ToRecordKeepsAllFields()
    {
        var register = RegisterMapper.ToDomain(CreateValidRecord());

        var record = RegisterMapper.ToRecord(register);

        Assert.Equal(register.Id.Value, record.Id);
        Assert.Equal(register.BranchId.Value, record.BranchId);
        Assert.Equal(register.Name, record.Name);
        Assert.Equal(register.Code, record.Code);
        Assert.Equal(register.IsActive, record.IsActive);
        Assert.Equal(register.CreatedAtUtc, record.CreatedAtUtc);
    }

    [Fact]
    public void ToRecordRejectsNullRegister()
    {
        Assert.Throws<ArgumentNullException>(() => RegisterMapper.ToRecord(null!));
    }

    // ---------- UpdateRecord ----------

    [Fact]
    public void UpdateRecordUpdatesNameAndCode()
    {
        var record = CreateValidRecord();
        var register = RegisterMapper.ToDomain(record);
        register.Rename("Caja Principal");
        register.ChangeCode("CAJA-2");

        RegisterMapper.UpdateRecord(register, record);

        Assert.Equal("Caja Principal", record.Name);
        Assert.Equal("CAJA-2", record.Code);
    }

    [Fact]
    public void UpdateRecordUpdatesIsActive()
    {
        var record = CreateValidRecord();
        var register = RegisterMapper.ToDomain(record);
        register.Deactivate();

        RegisterMapper.UpdateRecord(register, record);

        Assert.False(record.IsActive);
    }

    [Fact]
    public void UpdateRecordDoesNotChangeId()
    {
        var record = CreateValidRecord();
        var originalId = record.Id;
        var register = RegisterMapper.ToDomain(record);

        RegisterMapper.UpdateRecord(register, record);

        Assert.Equal(originalId, record.Id);
    }

    [Fact]
    public void UpdateRecordDoesNotChangeBranchId()
    {
        var record = CreateValidRecord();
        var originalBranchId = record.BranchId;
        var register = RegisterMapper.ToDomain(record);

        RegisterMapper.UpdateRecord(register, record);

        Assert.Equal(originalBranchId, record.BranchId);
    }

    [Fact]
    public void UpdateRecordDoesNotChangeCreatedAtUtc()
    {
        var record = CreateValidRecord();
        var register = RegisterMapper.ToDomain(record);

        RegisterMapper.UpdateRecord(register, record);

        Assert.Equal(CreatedAtUtc, record.CreatedAtUtc);
    }

    [Fact]
    public void UpdateRecordRejectsMismatchedId()
    {
        var record = CreateValidRecord();
        var register = RegisterMapper.ToDomain(record);
        record.Id = Guid.NewGuid();

        Assert.Throws<PersistenceDataException>(() => RegisterMapper.UpdateRecord(register, record));
    }

    [Fact]
    public void UpdateRecordRejectsMismatchedBranchId()
    {
        var record = CreateValidRecord();
        var register = RegisterMapper.ToDomain(record);
        record.BranchId = Guid.NewGuid();

        Assert.Throws<PersistenceDataException>(() => RegisterMapper.UpdateRecord(register, record));
    }

    [Fact]
    public void UpdateRecordRejectsMismatchedCreatedAtUtc()
    {
        var record = CreateValidRecord();
        var register = RegisterMapper.ToDomain(record);
        record.CreatedAtUtc = CreatedAtUtc.AddDays(1);

        Assert.Throws<PersistenceDataException>(() => RegisterMapper.UpdateRecord(register, record));
    }

    [Fact]
    public void UpdateRecordRejectsNullRegister()
    {
        var record = CreateValidRecord();

        Assert.Throws<ArgumentNullException>(() => RegisterMapper.UpdateRecord(null!, record));
    }

    [Fact]
    public void UpdateRecordRejectsNullRecord()
    {
        var register = RegisterMapper.ToDomain(CreateValidRecord());

        Assert.Throws<ArgumentNullException>(() => RegisterMapper.UpdateRecord(register, null!));
    }
}
