using Pos.Domain.RegisterSessions;
using Pos.Infrastructure.Persistence.Exceptions;
using Pos.Infrastructure.Persistence.Mappers;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Tests.Persistence.Mappers;

public class RegisterSessionMapperTests
{
    private static readonly DateTimeOffset OpenedAtUtc = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ClosedAtUtc = new(2026, 1, 1, 20, 0, 0, TimeSpan.Zero);

    private static RegisterSessionRecord CreateValidOpenRecord() => new()
    {
        Id = Guid.NewGuid(),
        RegisterId = Guid.NewGuid(),
        OpenedByUserId = Guid.NewGuid(),
        ClosedByUserId = null,
        OpeningFloatAmount = 100m,
        OpeningFloatCurrency = "USD",
        ExpectedCashAmount = null,
        ExpectedCashCurrency = null,
        CountedCashAmount = null,
        CountedCashCurrency = null,
        CashDifferenceAmount = null,
        CashDifferenceCurrency = null,
        Status = RegisterSessionStatus.Open,
        OpenedAtUtc = OpenedAtUtc,
        ClosedAtUtc = null,
    };

    private static RegisterSessionRecord CreateValidClosedRecord()
    {
        var record = CreateValidOpenRecord();
        record.ClosedByUserId = Guid.NewGuid();
        record.ExpectedCashAmount = 100m;
        record.ExpectedCashCurrency = "USD";
        record.CountedCashAmount = 110m;
        record.CountedCashCurrency = "USD";
        record.CashDifferenceAmount = 10m;
        record.CashDifferenceCurrency = "USD";
        record.Status = RegisterSessionStatus.Closed;
        record.ClosedAtUtc = ClosedAtUtc;
        return record;
    }

    // ---------- ToDomain: Open ----------

    [Fact]
    public void ToDomainOpenReconstructsIdentifiersAndOpeningFloat()
    {
        var record = CreateValidOpenRecord();

        var session = RegisterSessionMapper.ToDomain(record);

        Assert.Equal(record.Id, session.Id.Value);
        Assert.Equal(record.RegisterId, session.RegisterId.Value);
        Assert.Equal(record.OpenedByUserId, session.OpenedByUserId.Value);
        Assert.Equal(record.OpeningFloatAmount, session.OpeningFloat.Amount);
        Assert.Equal(record.OpeningFloatCurrency, session.OpeningFloat.Currency);
        Assert.Equal(RegisterSessionStatus.Open, session.Status);
    }

    [Fact]
    public void ToDomainOpenLeavesClosureDataNull()
    {
        var record = CreateValidOpenRecord();

        var session = RegisterSessionMapper.ToDomain(record);

        Assert.Null(session.ClosedByUserId);
        Assert.Null(session.ExpectedCash);
        Assert.Null(session.CountedCash);
        Assert.Null(session.CashDifference);
        Assert.Null(session.ClosedAtUtc);
    }

    // ---------- ToDomain: Closed ----------

    [Fact]
    public void ToDomainClosedReconstructsClosureFields()
    {
        var record = CreateValidClosedRecord();

        var session = RegisterSessionMapper.ToDomain(record);

        Assert.Equal(RegisterSessionStatus.Closed, session.Status);
        Assert.Equal(record.ClosedByUserId, session.ClosedByUserId!.Value.Value);
        Assert.Equal(record.ExpectedCashAmount, session.ExpectedCash!.Amount);
        Assert.Equal(record.CountedCashAmount, session.CountedCash!.Amount);
        Assert.Equal(record.CashDifferenceAmount, session.CashDifference!.Amount);
        Assert.Equal(record.ClosedAtUtc, session.ClosedAtUtc);
    }

    [Fact]
    public void ToDomainClosedRejectsCashDifferenceThatDoesNotMatchExpectedAndCountedCash()
    {
        var record = CreateValidClosedRecord();
        record.CashDifferenceAmount = 999m;

        Assert.Throws<PersistenceDataException>(() => RegisterSessionMapper.ToDomain(record));
    }

    // ---------- ToDomain: datos corruptos ----------

    [Fact]
    public void ToDomainRejectsNullRecord()
    {
        Assert.Throws<ArgumentNullException>(() => RegisterSessionMapper.ToDomain(null!));
    }

    [Fact]
    public void ToDomainWrapsEmptyIdInPersistenceDataException()
    {
        var record = CreateValidOpenRecord();
        record.Id = Guid.Empty;

        Assert.Throws<PersistenceDataException>(() => RegisterSessionMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsEmptyRegisterIdInPersistenceDataException()
    {
        var record = CreateValidOpenRecord();
        record.RegisterId = Guid.Empty;

        Assert.Throws<PersistenceDataException>(() => RegisterSessionMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsEmptyOpenedByUserIdInPersistenceDataException()
    {
        var record = CreateValidOpenRecord();
        record.OpenedByUserId = Guid.Empty;

        Assert.Throws<PersistenceDataException>(() => RegisterSessionMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsInvalidOpeningFloatCurrencyInPersistenceDataException()
    {
        var record = CreateValidOpenRecord();
        record.OpeningFloatCurrency = "US";

        Assert.Throws<PersistenceDataException>(() => RegisterSessionMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsUndefinedStatusInPersistenceDataException()
    {
        var record = CreateValidOpenRecord();
        record.Status = (RegisterSessionStatus)99;

        Assert.Throws<PersistenceDataException>(() => RegisterSessionMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsOpenStatusWithCloseDataInPersistenceDataException()
    {
        var record = CreateValidOpenRecord();
        record.ClosedAtUtc = ClosedAtUtc;

        Assert.Throws<PersistenceDataException>(() => RegisterSessionMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsClosedStatusMissingClosedByUserIdInPersistenceDataException()
    {
        var record = CreateValidClosedRecord();
        record.ClosedByUserId = null;

        Assert.Throws<PersistenceDataException>(() => RegisterSessionMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsClosedStatusMissingClosedAtUtcInPersistenceDataException()
    {
        var record = CreateValidClosedRecord();
        record.ClosedAtUtc = null;

        Assert.Throws<PersistenceDataException>(() => RegisterSessionMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsClosedAtUtcBeforeOpenedAtUtcInPersistenceDataException()
    {
        var record = CreateValidClosedRecord();
        record.ClosedAtUtc = OpenedAtUtc.AddHours(-1);

        Assert.Throws<PersistenceDataException>(() => RegisterSessionMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainThrowsPersistenceDataExceptionWhenExpectedCashAmountPresentWithoutCurrency()
    {
        var record = CreateValidClosedRecord();
        record.ExpectedCashCurrency = null;

        Assert.Throws<PersistenceDataException>(() => RegisterSessionMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainThrowsPersistenceDataExceptionWhenCashDifferenceCurrencyMismatchesOpeningFloat()
    {
        var record = CreateValidClosedRecord();
        record.CashDifferenceCurrency = "EUR";

        Assert.Throws<PersistenceDataException>(() => RegisterSessionMapper.ToDomain(record));
    }

    // ---------- ToRecord ----------

    [Fact]
    public void ToRecordKeepsOpenFields()
    {
        var session = RegisterSessionMapper.ToDomain(CreateValidOpenRecord());

        var record = RegisterSessionMapper.ToRecord(session);

        Assert.Equal(session.Id.Value, record.Id);
        Assert.Equal(session.RegisterId.Value, record.RegisterId);
        Assert.Equal(session.OpenedByUserId.Value, record.OpenedByUserId);
        Assert.Equal(session.OpeningFloat.Amount, record.OpeningFloatAmount);
        Assert.Equal(session.OpeningFloat.Currency, record.OpeningFloatCurrency);
        Assert.Equal(RegisterSessionStatus.Open, record.Status);
        Assert.Null(record.ClosedByUserId);
        Assert.Null(record.ExpectedCashAmount);
        Assert.Null(record.CountedCashAmount);
        Assert.Null(record.CashDifferenceAmount);
        Assert.Null(record.ClosedAtUtc);
    }

    [Fact]
    public void ToRecordKeepsClosedFields()
    {
        var session = RegisterSessionMapper.ToDomain(CreateValidClosedRecord());

        var record = RegisterSessionMapper.ToRecord(session);

        Assert.Equal(RegisterSessionStatus.Closed, record.Status);
        Assert.Equal(session.ClosedByUserId!.Value.Value, record.ClosedByUserId);
        Assert.Equal(session.ExpectedCash!.Amount, record.ExpectedCashAmount);
        Assert.Equal(session.CountedCash!.Amount, record.CountedCashAmount);
        Assert.Equal(session.CashDifference!.Amount, record.CashDifferenceAmount);
        Assert.Equal(session.ClosedAtUtc, record.ClosedAtUtc);
    }

    [Fact]
    public void ToRecordRejectsNullSession()
    {
        Assert.Throws<ArgumentNullException>(() => RegisterSessionMapper.ToRecord(null!));
    }

    // ---------- UpdateRecord ----------

    [Fact]
    public void UpdateRecordSyncsCloseTransition()
    {
        var record = CreateValidOpenRecord();
        var session = RegisterSessionMapper.ToDomain(record);

        var closedByUserId = Pos.Domain.Common.Identifiers.UserId.New();
        session.Close(
            closedByUserId,
            new Pos.Domain.Common.ValueObjects.Money(100m, "USD"),
            new Pos.Domain.Common.ValueObjects.Money(110m, "USD"),
            ClosedAtUtc);

        RegisterSessionMapper.UpdateRecord(session, record);

        Assert.Equal(RegisterSessionStatus.Closed, record.Status);
        Assert.Equal(closedByUserId.Value, record.ClosedByUserId);
        Assert.Equal(100m, record.ExpectedCashAmount);
        Assert.Equal(110m, record.CountedCashAmount);
        Assert.Equal(10m, record.CashDifferenceAmount);
        Assert.Equal(ClosedAtUtc, record.ClosedAtUtc);
    }

    // ---------- UpdateRecord: transiciones de Status ----------

    [Fact]
    public void UpdateRecordOpenToOpenPermittedWithoutCloseMutation()
    {
        var record = CreateValidOpenRecord();
        var session = RegisterSessionMapper.ToDomain(record);

        RegisterSessionMapper.UpdateRecord(session, record);

        Assert.Equal(RegisterSessionStatus.Open, record.Status);
        Assert.Null(record.ClosedByUserId);
        Assert.Null(record.ExpectedCashAmount);
        Assert.Null(record.ExpectedCashCurrency);
        Assert.Null(record.CountedCashAmount);
        Assert.Null(record.CountedCashCurrency);
        Assert.Null(record.CashDifferenceAmount);
        Assert.Null(record.CashDifferenceCurrency);
        Assert.Null(record.ClosedAtUtc);
    }

    [Fact]
    public void UpdateRecordClosedToClosedIdenticalPermitted()
    {
        var record = CreateValidClosedRecord();
        var session = RegisterSessionMapper.ToDomain(record);

        RegisterSessionMapper.UpdateRecord(session, record);

        Assert.Equal(RegisterSessionStatus.Closed, record.Status);
        Assert.Equal(session.ClosedByUserId!.Value.Value, record.ClosedByUserId);
        Assert.Equal(session.ExpectedCash!.Amount, record.ExpectedCashAmount);
        Assert.Equal(session.CountedCash!.Amount, record.CountedCashAmount);
        Assert.Equal(session.CashDifference!.Amount, record.CashDifferenceAmount);
        Assert.Equal(session.ClosedAtUtc, record.ClosedAtUtc);
    }

    [Fact]
    public void UpdateRecordClosedToOpenRejected()
    {
        var closedRecord = CreateValidClosedRecord();

        var openRecordWithSameIdentity = CreateValidOpenRecord();
        openRecordWithSameIdentity.Id = closedRecord.Id;
        openRecordWithSameIdentity.RegisterId = closedRecord.RegisterId;
        openRecordWithSameIdentity.OpenedByUserId = closedRecord.OpenedByUserId;
        openRecordWithSameIdentity.OpeningFloatAmount = closedRecord.OpeningFloatAmount;
        openRecordWithSameIdentity.OpeningFloatCurrency = closedRecord.OpeningFloatCurrency;
        openRecordWithSameIdentity.OpenedAtUtc = closedRecord.OpenedAtUtc;

        var openSession = RegisterSessionMapper.ToDomain(openRecordWithSameIdentity);

        Assert.Throws<PersistenceDataException>(() => RegisterSessionMapper.UpdateRecord(openSession, closedRecord));
    }

    [Fact]
    public void UpdateRecordClosedToOpenRejectedLeavesRecordIntact()
    {
        var closedRecord = CreateValidClosedRecord();

        var openRecordWithSameIdentity = CreateValidOpenRecord();
        openRecordWithSameIdentity.Id = closedRecord.Id;
        openRecordWithSameIdentity.RegisterId = closedRecord.RegisterId;
        openRecordWithSameIdentity.OpenedByUserId = closedRecord.OpenedByUserId;
        openRecordWithSameIdentity.OpeningFloatAmount = closedRecord.OpeningFloatAmount;
        openRecordWithSameIdentity.OpeningFloatCurrency = closedRecord.OpeningFloatCurrency;
        openRecordWithSameIdentity.OpenedAtUtc = closedRecord.OpenedAtUtc;

        var openSession = RegisterSessionMapper.ToDomain(openRecordWithSameIdentity);

        var originalStatus = closedRecord.Status;
        var originalClosedByUserId = closedRecord.ClosedByUserId;
        var originalExpectedCashAmount = closedRecord.ExpectedCashAmount;
        var originalCountedCashAmount = closedRecord.CountedCashAmount;
        var originalCashDifferenceAmount = closedRecord.CashDifferenceAmount;
        var originalClosedAtUtc = closedRecord.ClosedAtUtc;

        Assert.Throws<PersistenceDataException>(() => RegisterSessionMapper.UpdateRecord(openSession, closedRecord));

        Assert.Equal(originalStatus, closedRecord.Status);
        Assert.Equal(originalClosedByUserId, closedRecord.ClosedByUserId);
        Assert.Equal(originalExpectedCashAmount, closedRecord.ExpectedCashAmount);
        Assert.Equal(originalCountedCashAmount, closedRecord.CountedCashAmount);
        Assert.Equal(originalCashDifferenceAmount, closedRecord.CashDifferenceAmount);
        Assert.Equal(originalClosedAtUtc, closedRecord.ClosedAtUtc);
    }

    [Fact]
    public void UpdateRecordClosedToClosedDifferentClosedByUserIdRejected()
    {
        var record = CreateValidClosedRecord();
        var session = RegisterSessionMapper.ToDomain(record);
        var originalClosedByUserId = record.ClosedByUserId;

        record.ClosedByUserId = Guid.NewGuid();

        Assert.Throws<PersistenceDataException>(() => RegisterSessionMapper.UpdateRecord(session, record));
        Assert.NotEqual(originalClosedByUserId, record.ClosedByUserId);
    }

    [Fact]
    public void UpdateRecordClosedToClosedDifferentExpectedCashRejected()
    {
        var record = CreateValidClosedRecord();
        var session = RegisterSessionMapper.ToDomain(record);

        record.ExpectedCashAmount = 999m;

        Assert.Throws<PersistenceDataException>(() => RegisterSessionMapper.UpdateRecord(session, record));
    }

    [Fact]
    public void UpdateRecordClosedToClosedDifferentCountedCashRejected()
    {
        var record = CreateValidClosedRecord();
        var session = RegisterSessionMapper.ToDomain(record);

        record.CountedCashAmount = 999m;

        Assert.Throws<PersistenceDataException>(() => RegisterSessionMapper.UpdateRecord(session, record));
    }

    [Fact]
    public void UpdateRecordClosedToClosedDifferentCashDifferenceRejected()
    {
        var record = CreateValidClosedRecord();
        var session = RegisterSessionMapper.ToDomain(record);

        record.CashDifferenceAmount = 999m;

        Assert.Throws<PersistenceDataException>(() => RegisterSessionMapper.UpdateRecord(session, record));
    }

    [Fact]
    public void UpdateRecordClosedToClosedDifferentClosedAtUtcRejected()
    {
        var record = CreateValidClosedRecord();
        var session = RegisterSessionMapper.ToDomain(record);

        record.ClosedAtUtc = ClosedAtUtc.AddHours(1);

        Assert.Throws<PersistenceDataException>(() => RegisterSessionMapper.UpdateRecord(session, record));
    }

    [Fact]
    public void UpdateRecordClosedToClosedRejectedDueToMismatchLeavesRecordFieldsIntactExceptTheMismatchedOne()
    {
        var record = CreateValidClosedRecord();
        var session = RegisterSessionMapper.ToDomain(record);

        record.CountedCashAmount = 999m;

        var originalClosedByUserId = record.ClosedByUserId;
        var originalExpectedCashAmount = record.ExpectedCashAmount;
        var originalCashDifferenceAmount = record.CashDifferenceAmount;
        var originalClosedAtUtc = record.ClosedAtUtc;
        var originalStatus = record.Status;

        Assert.Throws<PersistenceDataException>(() => RegisterSessionMapper.UpdateRecord(session, record));

        Assert.Equal(originalClosedByUserId, record.ClosedByUserId);
        Assert.Equal(originalExpectedCashAmount, record.ExpectedCashAmount);
        Assert.Equal(originalCashDifferenceAmount, record.CashDifferenceAmount);
        Assert.Equal(originalClosedAtUtc, record.ClosedAtUtc);
        Assert.Equal(originalStatus, record.Status);
        Assert.Equal(999m, record.CountedCashAmount);
    }

    [Fact]
    public void UpdateRecordDoesNotChangeId()
    {
        var record = CreateValidOpenRecord();
        var originalId = record.Id;
        var session = RegisterSessionMapper.ToDomain(record);

        RegisterSessionMapper.UpdateRecord(session, record);

        Assert.Equal(originalId, record.Id);
    }

    [Fact]
    public void UpdateRecordDoesNotChangeOpeningFloat()
    {
        var record = CreateValidOpenRecord();
        var session = RegisterSessionMapper.ToDomain(record);

        RegisterSessionMapper.UpdateRecord(session, record);

        Assert.Equal(100m, record.OpeningFloatAmount);
        Assert.Equal("USD", record.OpeningFloatCurrency);
    }

    [Fact]
    public void UpdateRecordRejectsMismatchedId()
    {
        var record = CreateValidOpenRecord();
        var session = RegisterSessionMapper.ToDomain(record);
        record.Id = Guid.NewGuid();

        Assert.Throws<PersistenceDataException>(() => RegisterSessionMapper.UpdateRecord(session, record));
    }

    [Fact]
    public void UpdateRecordRejectsMismatchedRegisterId()
    {
        var record = CreateValidOpenRecord();
        var session = RegisterSessionMapper.ToDomain(record);
        record.RegisterId = Guid.NewGuid();

        Assert.Throws<PersistenceDataException>(() => RegisterSessionMapper.UpdateRecord(session, record));
    }

    [Fact]
    public void UpdateRecordRejectsMismatchedOpenedByUserId()
    {
        var record = CreateValidOpenRecord();
        var session = RegisterSessionMapper.ToDomain(record);
        record.OpenedByUserId = Guid.NewGuid();

        Assert.Throws<PersistenceDataException>(() => RegisterSessionMapper.UpdateRecord(session, record));
    }

    [Fact]
    public void UpdateRecordRejectsMismatchedOpeningFloat()
    {
        var record = CreateValidOpenRecord();
        var session = RegisterSessionMapper.ToDomain(record);
        record.OpeningFloatAmount = 999m;

        Assert.Throws<PersistenceDataException>(() => RegisterSessionMapper.UpdateRecord(session, record));
    }

    [Fact]
    public void UpdateRecordRejectsMismatchedOpenedAtUtc()
    {
        var record = CreateValidOpenRecord();
        var session = RegisterSessionMapper.ToDomain(record);
        record.OpenedAtUtc = OpenedAtUtc.AddDays(1);

        Assert.Throws<PersistenceDataException>(() => RegisterSessionMapper.UpdateRecord(session, record));
    }

    [Fact]
    public void UpdateRecordRejectsNullSession()
    {
        var record = CreateValidOpenRecord();

        Assert.Throws<ArgumentNullException>(() => RegisterSessionMapper.UpdateRecord(null!, record));
    }

    [Fact]
    public void UpdateRecordRejectsNullRecord()
    {
        var session = RegisterSessionMapper.ToDomain(CreateValidOpenRecord());

        Assert.Throws<ArgumentNullException>(() => RegisterSessionMapper.UpdateRecord(session, null!));
    }
}
