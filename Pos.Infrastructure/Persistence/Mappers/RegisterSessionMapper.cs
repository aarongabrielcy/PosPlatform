using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Common.ValueObjects;
using Pos.Domain.RegisterSessions;
using Pos.Infrastructure.Persistence.Exceptions;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Persistence.Mappers;

internal static class RegisterSessionMapper
{
    internal static RegisterSession ToDomain(RegisterSessionRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        try
        {
            var openingFloat = new Money(record.OpeningFloatAmount, record.OpeningFloatCurrency);
            var expectedCash = ToMoney(record.ExpectedCashAmount, record.ExpectedCashCurrency, "ExpectedCash", record.Id);
            var countedCash = ToMoney(record.CountedCashAmount, record.CountedCashCurrency, "CountedCash", record.Id);
            var cashDifference = ToMoney(
                record.CashDifferenceAmount, record.CashDifferenceCurrency, "CashDifference", record.Id);

            return RegisterSession.Rehydrate(
                new RegisterSessionId(record.Id),
                new RegisterId(record.RegisterId),
                new UserId(record.OpenedByUserId),
                openingFloat,
                record.OpenedAtUtc,
                record.Status,
                record.ClosedByUserId is null ? null : new UserId(record.ClosedByUserId.Value),
                expectedCash,
                countedCash,
                cashDifference,
                record.ClosedAtUtc);
        }
        catch (DomainValidationException ex)
        {
            throw new PersistenceDataException(
                $"RegisterSessionRecord con Id '{record.Id}' contiene datos inválidos: {ex.Message}", ex);
        }
    }

    internal static RegisterSessionRecord ToRecord(RegisterSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return new RegisterSessionRecord
        {
            Id = session.Id.Value,
            RegisterId = session.RegisterId.Value,
            OpenedByUserId = session.OpenedByUserId.Value,
            ClosedByUserId = session.ClosedByUserId?.Value,
            OpeningFloatAmount = session.OpeningFloat.Amount,
            OpeningFloatCurrency = session.OpeningFloat.Currency,
            ExpectedCashAmount = session.ExpectedCash?.Amount,
            ExpectedCashCurrency = session.ExpectedCash?.Currency,
            CountedCashAmount = session.CountedCash?.Amount,
            CountedCashCurrency = session.CountedCash?.Currency,
            CashDifferenceAmount = session.CashDifference?.Amount,
            CashDifferenceCurrency = session.CashDifference?.Currency,
            Status = session.Status,
            OpenedAtUtc = session.OpenedAtUtc,
            ClosedAtUtc = session.ClosedAtUtc,
        };
    }

    // Id, RegisterId, OpenedByUserId, OpeningFloat y OpenedAtUtc son inmutables en Domain.
    // Transiciones permitidas de Status: Open(record) -> Open(domain), Open(record) -> Closed(domain)
    // (representa Close) y Closed(record) -> Closed(domain) solo si todos los datos históricos de
    // cierre coinciden exactamente. Closed(record) -> Open(domain) se rechaza: una sesión cerrada
    // no puede reabrirse ni sus datos históricos pueden modificarse mediante UpdateRecord.
    internal static void UpdateRecord(RegisterSession session, RegisterSessionRecord record)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(record);

        // ---------- Validar sin mutar ----------

        if (record.Id != session.Id.Value)
        {
            throw new PersistenceDataException(
                $"No se puede actualizar RegisterSessionRecord '{record.Id}': Id no coincide con RegisterSession '{session.Id}'.");
        }

        if (record.RegisterId != session.RegisterId.Value)
        {
            throw new PersistenceDataException(
                $"No se puede actualizar RegisterSessionRecord '{record.Id}': RegisterId no coincide.");
        }

        if (record.OpenedByUserId != session.OpenedByUserId.Value)
        {
            throw new PersistenceDataException(
                $"No se puede actualizar RegisterSessionRecord '{record.Id}': OpenedByUserId no coincide.");
        }

        if (record.OpeningFloatAmount != session.OpeningFloat.Amount
            || !string.Equals(record.OpeningFloatCurrency, session.OpeningFloat.Currency, StringComparison.Ordinal))
        {
            throw new PersistenceDataException(
                $"No se puede actualizar RegisterSessionRecord '{record.Id}': OpeningFloat no coincide.");
        }

        if (record.OpenedAtUtc != session.OpenedAtUtc)
        {
            throw new PersistenceDataException(
                $"No se puede actualizar RegisterSessionRecord '{record.Id}': OpenedAtUtc no coincide.");
        }

        if (record.Status == RegisterSessionStatus.Closed && session.Status == RegisterSessionStatus.Open)
        {
            throw new PersistenceDataException(
                $"No se puede actualizar RegisterSessionRecord '{record.Id}': una sesión Closed no puede volver a Open.");
        }

        if (record.Status == RegisterSessionStatus.Closed && session.Status == RegisterSessionStatus.Closed)
        {
            EnsureClosedHistoricalDataUnchanged(session, record);
        }

        // ---------- Mutar ----------

        record.ClosedByUserId = session.ClosedByUserId?.Value;
        record.ExpectedCashAmount = session.ExpectedCash?.Amount;
        record.ExpectedCashCurrency = session.ExpectedCash?.Currency;
        record.CountedCashAmount = session.CountedCash?.Amount;
        record.CountedCashCurrency = session.CountedCash?.Currency;
        record.CashDifferenceAmount = session.CashDifference?.Amount;
        record.CashDifferenceCurrency = session.CashDifference?.Currency;
        record.Status = session.Status;
        record.ClosedAtUtc = session.ClosedAtUtc;
    }

    private static void EnsureClosedHistoricalDataUnchanged(RegisterSession session, RegisterSessionRecord record)
    {
        if (record.ClosedByUserId != session.ClosedByUserId?.Value)
        {
            throw new PersistenceDataException(
                $"No se puede actualizar RegisterSessionRecord '{record.Id}': ClosedByUserId de una sesión cerrada no puede cambiar.");
        }

        EnsureClosedMoneyUnchanged(
            record.ExpectedCashAmount, record.ExpectedCashCurrency, session.ExpectedCash, "ExpectedCash", record.Id);
        EnsureClosedMoneyUnchanged(
            record.CountedCashAmount, record.CountedCashCurrency, session.CountedCash, "CountedCash", record.Id);
        EnsureClosedMoneyUnchanged(
            record.CashDifferenceAmount, record.CashDifferenceCurrency, session.CashDifference, "CashDifference", record.Id);

        if (record.ClosedAtUtc != session.ClosedAtUtc)
        {
            throw new PersistenceDataException(
                $"No se puede actualizar RegisterSessionRecord '{record.Id}': ClosedAtUtc de una sesión cerrada no puede cambiar.");
        }

        if (record.Status != session.Status)
        {
            throw new PersistenceDataException(
                $"No se puede actualizar RegisterSessionRecord '{record.Id}': Status de una sesión cerrada no puede cambiar.");
        }
    }

    private static void EnsureClosedMoneyUnchanged(
        decimal? recordAmount, string? recordCurrency, Money? sessionMoney, string fieldName, Guid recordId)
    {
        if (recordAmount != sessionMoney?.Amount
            || !string.Equals(recordCurrency, sessionMoney?.Currency, StringComparison.Ordinal))
        {
            throw new PersistenceDataException(
                $"No se puede actualizar RegisterSessionRecord '{recordId}': {fieldName} de una sesión cerrada no puede cambiar.");
        }
    }

    private static Money? ToMoney(decimal? amount, string? currency, string fieldName, Guid recordId)
    {
        if (amount is null && currency is null)
        {
            return null;
        }

        if (amount is null || currency is null)
        {
            throw new PersistenceDataException(
                $"RegisterSessionRecord con Id '{recordId}' tiene datos incompletos en '{fieldName}': " +
                "amount y currency deben ser ambos nulos o ambos tener valor.");
        }

        return new Money(amount.Value, currency);
    }
}
