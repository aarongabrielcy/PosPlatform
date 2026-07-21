using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Common.ValueObjects;
using Pos.Domain.Products;
using Pos.Domain.Sales;
using Pos.Infrastructure.Persistence.Exceptions;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Persistence.Mappers;

internal static class SaleMapper
{
    internal static Sale ToDomain(SaleRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        try
        {
            var lines = record.Lines
                .OrderBy(line => line.Id)
                .Select(line => ToDomainLine(record.Id, line))
                .ToList();

            var payments = record.Payments
                .OrderBy(payment => payment.PaidAtUtc)
                .ThenBy(payment => payment.Id)
                .Select(payment => ToDomainPayment(record.Id, payment))
                .ToList();

            return Sale.Rehydrate(
                new SaleId(record.Id),
                new OrganizationId(record.OrganizationId),
                new BranchId(record.BranchId),
                new RegisterSessionId(record.RegisterSessionId),
                new UserId(record.CreatedByUserId),
                record.Currency,
                record.CreatedAtUtc,
                lines,
                payments,
                record.Status,
                record.CompletedAtUtc);
        }
        catch (DomainValidationException ex)
        {
            throw new PersistenceDataException(
                $"SaleRecord con Id '{record.Id}' contiene datos inválidos: {ex.Message}", ex);
        }
    }

    internal static SaleRecord ToRecord(Sale sale)
    {
        ArgumentNullException.ThrowIfNull(sale);

        var record = new SaleRecord
        {
            Id = sale.Id.Value,
            OrganizationId = sale.OrganizationId.Value,
            BranchId = sale.BranchId.Value,
            RegisterSessionId = sale.RegisterSessionId.Value,
            CreatedByUserId = sale.CreatedByUserId.Value,
            Currency = sale.Subtotal.Currency,
            Status = sale.Status,
            CreatedAtUtc = sale.CreatedAtUtc,
            CompletedAtUtc = sale.CompletedAtUtc,
        };

        foreach (var line in sale.Lines)
        {
            record.Lines.Add(ToLineRecord(line, record));
        }

        foreach (var payment in sale.Payments)
        {
            record.Payments.Add(ToPaymentRecord(payment, record));
        }

        return record;
    }

    // Sincroniza SaleRecord en dos fases: primero valida todo el grafo (encabezado,
    // duplicados e inmutables de hijos existentes) sin mutar nada; solo si toda la
    // validación pasa se procede a mutar record y sus colecciones. Así, ante cualquier
    // PersistenceDataException, el ChangeTracker queda exactamente como estaba.
    internal static void UpdateRecord(Sale sale, SaleRecord record)
    {
        ArgumentNullException.ThrowIfNull(sale);
        ArgumentNullException.ThrowIfNull(record);

        // FASE A — validación (no debe mutar record ni sus colecciones).
        ValidateHeaderIdentity(sale, record);
        EnsureNoDuplicateIds(record);

        var lines = sale.Lines;
        var payments = sale.Payments;

        var saleLinesById = ValidateExistingLines(sale, record, lines);
        var salePaymentsById = ValidateExistingPayments(sale, record, payments);

        // FASE B — sincronización (toda la validación ya pasó).
        record.Status = sale.Status;
        record.CompletedAtUtc = sale.CompletedAtUtc;

        SyncLines(record, saleLinesById);
        SyncPayments(record, salePaymentsById);
    }

    private static void ValidateHeaderIdentity(Sale sale, SaleRecord record)
    {
        if (record.Id != sale.Id.Value)
        {
            throw new PersistenceDataException(
                $"No se puede actualizar SaleRecord '{record.Id}': Id no coincide con Sale '{sale.Id}'.");
        }

        if (record.OrganizationId != sale.OrganizationId.Value)
        {
            throw new PersistenceDataException(
                $"No se puede actualizar SaleRecord '{record.Id}': OrganizationId no coincide.");
        }

        if (record.BranchId != sale.BranchId.Value)
        {
            throw new PersistenceDataException(
                $"No se puede actualizar SaleRecord '{record.Id}': BranchId no coincide.");
        }

        if (record.RegisterSessionId != sale.RegisterSessionId.Value)
        {
            throw new PersistenceDataException(
                $"No se puede actualizar SaleRecord '{record.Id}': RegisterSessionId no coincide.");
        }

        if (record.CreatedByUserId != sale.CreatedByUserId.Value)
        {
            throw new PersistenceDataException(
                $"No se puede actualizar SaleRecord '{record.Id}': CreatedByUserId no coincide.");
        }

        if (record.CreatedAtUtc != sale.CreatedAtUtc)
        {
            throw new PersistenceDataException(
                $"No se puede actualizar SaleRecord '{record.Id}': CreatedAtUtc no coincide.");
        }

        if (!string.Equals(record.Currency, sale.Subtotal.Currency, StringComparison.Ordinal))
        {
            throw new PersistenceDataException(
                $"No se puede actualizar SaleRecord '{record.Id}': Currency no coincide.");
        }
    }

    private static void EnsureNoDuplicateIds(SaleRecord record)
    {
        var duplicateLine = record.Lines
            .GroupBy(lineRecord => lineRecord.Id)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateLine is not null)
        {
            throw new PersistenceDataException(
                $"SaleRecord con Id '{record.Id}' contiene líneas duplicadas con Id '{duplicateLine.Key}'.");
        }

        var duplicatePayment = record.Payments
            .GroupBy(paymentRecord => paymentRecord.Id)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicatePayment is not null)
        {
            throw new PersistenceDataException(
                $"SaleRecord con Id '{record.Id}' contiene pagos duplicados con Id '{duplicatePayment.Key}'.");
        }
    }

    // Las únicas propiedades modificables de una SaleLineRecord existente son Quantity;
    // Id, SaleId, ProductId, ProductSku, ProductName, UnitPriceAmount y Currency son
    // snapshots históricos inmutables.
    private static Dictionary<Guid, SaleLine> ValidateExistingLines(
        Sale sale, SaleRecord record, IReadOnlyCollection<SaleLine> lines)
    {
        var saleLinesById = lines.ToDictionary(line => line.Id.Value);

        foreach (var lineRecord in record.Lines)
        {
            if (!saleLinesById.TryGetValue(lineRecord.Id, out var line))
            {
                continue;
            }

            if (lineRecord.SaleId != sale.Id.Value)
            {
                throw new PersistenceDataException(
                    $"No se puede actualizar SaleLineRecord '{lineRecord.Id}': SaleId no coincide.");
            }

            if (lineRecord.ProductId != line.ProductId.Value)
            {
                throw new PersistenceDataException(
                    $"No se puede actualizar SaleLineRecord '{lineRecord.Id}': ProductId no coincide.");
            }

            if (!string.Equals(lineRecord.ProductSku, line.ProductSku.Value, StringComparison.Ordinal))
            {
                throw new PersistenceDataException(
                    $"No se puede actualizar SaleLineRecord '{lineRecord.Id}': ProductSku no coincide.");
            }

            if (!string.Equals(lineRecord.ProductName, line.ProductName, StringComparison.Ordinal))
            {
                throw new PersistenceDataException(
                    $"No se puede actualizar SaleLineRecord '{lineRecord.Id}': ProductName no coincide.");
            }

            if (lineRecord.UnitPriceAmount != line.UnitPrice.Amount)
            {
                throw new PersistenceDataException(
                    $"No se puede actualizar SaleLineRecord '{lineRecord.Id}': UnitPriceAmount no coincide.");
            }

            if (!string.Equals(lineRecord.Currency, line.UnitPrice.Currency, StringComparison.Ordinal))
            {
                throw new PersistenceDataException(
                    $"No se puede actualizar SaleLineRecord '{lineRecord.Id}': Currency no coincide.");
            }
        }

        return saleLinesById;
    }

    // Payment es inmutable en el dominio: un PaymentRecord existente debe coincidir
    // exactamente en todos sus campos, o la sincronización se rechaza.
    private static Dictionary<Guid, Payment> ValidateExistingPayments(
        Sale sale, SaleRecord record, IReadOnlyCollection<Payment> payments)
    {
        var salePaymentsById = payments.ToDictionary(payment => payment.Id.Value);

        foreach (var paymentRecord in record.Payments)
        {
            if (!salePaymentsById.TryGetValue(paymentRecord.Id, out var payment))
            {
                continue;
            }

            if (paymentRecord.SaleId != sale.Id.Value)
            {
                throw new PersistenceDataException(
                    $"No se puede actualizar PaymentRecord '{paymentRecord.Id}': SaleId no coincide.");
            }

            if (paymentRecord.Method != payment.Method)
            {
                throw new PersistenceDataException(
                    $"No se puede actualizar PaymentRecord '{paymentRecord.Id}': Method no coincide.");
            }

            if (paymentRecord.Amount != payment.Amount.Amount)
            {
                throw new PersistenceDataException(
                    $"No se puede actualizar PaymentRecord '{paymentRecord.Id}': Amount no coincide.");
            }

            if (!string.Equals(paymentRecord.Currency, payment.Amount.Currency, StringComparison.Ordinal))
            {
                throw new PersistenceDataException(
                    $"No se puede actualizar PaymentRecord '{paymentRecord.Id}': Currency no coincide.");
            }

            if (paymentRecord.PaidAtUtc != payment.PaidAtUtc)
            {
                throw new PersistenceDataException(
                    $"No se puede actualizar PaymentRecord '{paymentRecord.Id}': PaidAtUtc no coincide.");
            }
        }

        return salePaymentsById;
    }

    private static SaleLine ToDomainLine(Guid saleId, SaleLineRecord line)
    {
        if (line.SaleId != saleId)
        {
            throw new PersistenceDataException(
                $"SaleLineRecord con Id '{line.Id}' tiene SaleId '{line.SaleId}' distinto al de SaleRecord '{saleId}'.");
        }

        return new SaleLine(
            new SaleLineId(line.Id),
            new ProductId(line.ProductId),
            new Sku(line.ProductSku),
            line.ProductName,
            line.Quantity,
            new Money(line.UnitPriceAmount, line.Currency));
    }

    private static Payment ToDomainPayment(Guid saleId, PaymentRecord payment)
    {
        if (payment.SaleId != saleId)
        {
            throw new PersistenceDataException(
                $"PaymentRecord con Id '{payment.Id}' tiene SaleId '{payment.SaleId}' distinto al de SaleRecord '{saleId}'.");
        }

        return new Payment(
            new PaymentId(payment.Id),
            payment.Method,
            new Money(payment.Amount, payment.Currency),
            payment.PaidAtUtc);
    }

    private static SaleLineRecord ToLineRecord(SaleLine line, SaleRecord record) => new()
    {
        Id = line.Id.Value,
        SaleId = record.Id,
        ProductId = line.ProductId.Value,
        ProductSku = line.ProductSku.Value,
        ProductName = line.ProductName,
        Quantity = line.Quantity,
        UnitPriceAmount = line.UnitPrice.Amount,
        Currency = line.UnitPrice.Currency,
        Sale = record,
    };

    private static PaymentRecord ToPaymentRecord(Payment payment, SaleRecord record) => new()
    {
        Id = payment.Id.Value,
        SaleId = record.Id,
        Method = payment.Method,
        Amount = payment.Amount.Amount,
        Currency = payment.Amount.Currency,
        PaidAtUtc = payment.PaidAtUtc,
        Sale = record,
    };

    // Mutación pura: toda la validación ya ocurrió en ValidateExistingLines. La única
    // propiedad que se sincroniza en líneas existentes es Quantity.
    private static void SyncLines(SaleRecord record, Dictionary<Guid, SaleLine> saleLinesById)
    {
        record.Lines.RemoveAll(lineRecord => !saleLinesById.ContainsKey(lineRecord.Id));

        foreach (var lineRecord in record.Lines)
        {
            lineRecord.Quantity = saleLinesById[lineRecord.Id].Quantity;
        }

        var existingIds = record.Lines.Select(lineRecord => lineRecord.Id).ToHashSet();

        foreach (var (id, line) in saleLinesById)
        {
            if (existingIds.Contains(id))
            {
                continue;
            }

            record.Lines.Add(ToLineRecord(line, record));
        }
    }

    // Mutación pura: toda la validación ya ocurrió en ValidateExistingPayments. Payment
    // es inmutable, así que los pagos existentes nunca se sobrescriben.
    private static void SyncPayments(SaleRecord record, Dictionary<Guid, Payment> salePaymentsById)
    {
        record.Payments.RemoveAll(paymentRecord => !salePaymentsById.ContainsKey(paymentRecord.Id));

        var existingIds = record.Payments.Select(paymentRecord => paymentRecord.Id).ToHashSet();

        foreach (var (id, payment) in salePaymentsById)
        {
            if (existingIds.Contains(id))
            {
                continue;
            }

            record.Payments.Add(ToPaymentRecord(payment, record));
        }
    }
}
