using Pos.Domain.Common.Identifiers;
using Pos.Domain.Common.ValueObjects;
using Pos.Domain.Products;
using Pos.Domain.Sales;
using Pos.Infrastructure.Persistence.Exceptions;
using Pos.Infrastructure.Persistence.Mappers;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Tests.Persistence.Mappers;

public class SaleMapperTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CompletedAtUtc = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    private static SaleRecord CreateValidDraftRecord(Guid? saleId = null)
    {
        var id = saleId ?? Guid.NewGuid();

        var record = new SaleRecord
        {
            Id = id,
            OrganizationId = Guid.NewGuid(),
            BranchId = Guid.NewGuid(),
            RegisterSessionId = Guid.NewGuid(),
            CreatedByUserId = Guid.NewGuid(),
            Currency = "MXN",
            Status = SaleStatus.Draft,
            CreatedAtUtc = CreatedAtUtc,
            CompletedAtUtc = null,
        };

        record.Lines.Add(new SaleLineRecord
        {
            Id = Guid.NewGuid(),
            SaleId = id,
            ProductId = Guid.NewGuid(),
            ProductSku = "SKU-001",
            ProductName = "Producto de prueba",
            Quantity = 2m,
            UnitPriceAmount = 10m,
            Currency = "MXN",
            Sale = record,
        });

        record.Payments.Add(new PaymentRecord
        {
            Id = Guid.NewGuid(),
            SaleId = id,
            Method = PaymentMethod.Cash,
            Amount = 20m,
            Currency = "MXN",
            PaidAtUtc = CreatedAtUtc,
            Sale = record,
        });

        return record;
    }

    private static SaleRecord CreateValidCompletedRecord()
    {
        var record = CreateValidDraftRecord();
        record.Status = SaleStatus.Completed;
        record.CompletedAtUtc = CompletedAtUtc;
        return record;
    }

    // ---------- ToDomain ----------

    [Fact]
    public void ToDomainReconstructsIdentifiers()
    {
        var record = CreateValidDraftRecord();

        var sale = SaleMapper.ToDomain(record);

        Assert.Equal(record.Id, sale.Id.Value);
        Assert.Equal(record.OrganizationId, sale.OrganizationId.Value);
        Assert.Equal(record.BranchId, sale.BranchId.Value);
        Assert.Equal(record.RegisterSessionId, sale.RegisterSessionId.Value);
        Assert.Equal(record.CreatedByUserId, sale.CreatedByUserId.Value);
    }

    [Fact]
    public void ToDomainReconstructsDraft()
    {
        var record = CreateValidDraftRecord();

        var sale = SaleMapper.ToDomain(record);

        Assert.Equal(SaleStatus.Draft, sale.Status);
        Assert.Null(sale.CompletedAtUtc);
    }

    [Fact]
    public void ToDomainReconstructsCompleted()
    {
        var record = CreateValidCompletedRecord();

        var sale = SaleMapper.ToDomain(record);

        Assert.Equal(SaleStatus.Completed, sale.Status);
        Assert.Equal(CompletedAtUtc, sale.CompletedAtUtc);
    }

    [Fact]
    public void ToDomainReconstructsLineSnapshots()
    {
        var record = CreateValidDraftRecord();
        var lineRecord = record.Lines.Single();

        var sale = SaleMapper.ToDomain(record);

        var line = sale.Lines.Single();
        Assert.Equal(lineRecord.Id, line.Id.Value);
        Assert.Equal(lineRecord.ProductId, line.ProductId.Value);
        Assert.Equal(lineRecord.ProductSku, line.ProductSku.Value);
        Assert.Equal(lineRecord.ProductName, line.ProductName);
        Assert.Equal(lineRecord.Quantity, line.Quantity);
        Assert.Equal(new Money(lineRecord.UnitPriceAmount, lineRecord.Currency), line.UnitPrice);
    }

    [Fact]
    public void ToDomainReconstructsMultiplePayments()
    {
        var record = CreateValidDraftRecord();
        record.Lines.Single().Quantity = 10m;
        record.Payments.Clear();
        var firstPaymentId = Guid.NewGuid();
        var secondPaymentId = Guid.NewGuid();
        record.Payments.Add(new PaymentRecord
        {
            Id = firstPaymentId,
            SaleId = record.Id,
            Method = PaymentMethod.Card,
            Amount = 60m,
            Currency = "MXN",
            PaidAtUtc = CreatedAtUtc,
            Reference = "AUTH-0001",
            Sale = record,
        });
        record.Payments.Add(new PaymentRecord
        {
            Id = secondPaymentId,
            SaleId = record.Id,
            Method = PaymentMethod.Cash,
            Amount = 40m,
            Currency = "MXN",
            PaidAtUtc = CreatedAtUtc.AddMinutes(1),
            Sale = record,
        });

        var sale = SaleMapper.ToDomain(record);

        Assert.Equal(2, sale.Payments.Count);
        Assert.Contains(sale.Payments, p => p.Id.Value == firstPaymentId);
        Assert.Contains(sale.Payments, p => p.Id.Value == secondPaymentId);
    }

    // TAREA 25C-FIX sección 5-6: una fila PaymentRecord Card histórica con reference = NULL
    // (persistida antes de que la referencia fuera obligatoria para Card nuevos) debe poder
    // reconstruirse sin lanzar PersistenceDataException.
    [Fact]
    public void ToDomainReconstructsAHistoricalCardPaymentWithNullReferenceWithoutThrowing()
    {
        var record = CreateValidDraftRecord();
        record.Payments.Clear();
        record.Payments.Add(new PaymentRecord
        {
            Id = Guid.NewGuid(),
            SaleId = record.Id,
            Method = PaymentMethod.Card,
            Amount = 20m,
            Currency = "MXN",
            PaidAtUtc = CreatedAtUtc,
            Reference = null,
            Sale = record,
        });

        var sale = SaleMapper.ToDomain(record);

        var payment = Assert.Single(sale.Payments);
        Assert.Equal(PaymentMethod.Card, payment.Method);
        Assert.Null(payment.Reference);
    }

    // TAREA 25C-FIX sección 7: BankTransfer nunca tuvo una invariante de Reference nueva; una fila
    // sin referencia debe seguir siendo válida/legible igual que antes de este cambio.
    [Fact]
    public void ToDomainReconstructsABankTransferPaymentWithNullReferenceWithoutThrowing()
    {
        var record = CreateValidDraftRecord();
        record.Payments.Clear();
        record.Payments.Add(new PaymentRecord
        {
            Id = Guid.NewGuid(),
            SaleId = record.Id,
            Method = PaymentMethod.BankTransfer,
            Amount = 20m,
            Currency = "MXN",
            PaidAtUtc = CreatedAtUtc,
            Reference = null,
            Sale = record,
        });

        var sale = SaleMapper.ToDomain(record);

        var payment = Assert.Single(sale.Payments);
        Assert.Equal(PaymentMethod.BankTransfer, payment.Method);
        Assert.Null(payment.Reference);
    }

    [Fact]
    public void ToDomainRecalculatesTotalsFromLinesAndPayments()
    {
        var record = CreateValidDraftRecord();

        var sale = SaleMapper.ToDomain(record);

        Assert.Equal(new Money(20m, "MXN"), sale.Subtotal);
        Assert.Equal(new Money(20m, "MXN"), sale.Total);
        Assert.Equal(new Money(20m, "MXN"), sale.PaidAmount);
        Assert.Equal(new Money(0m, "MXN"), sale.BalanceDue);
    }

    [Fact]
    public void ToDomainReturnsLinesInDeterministicOrderById()
    {
        var record = CreateValidDraftRecord();
        record.Lines.Clear();
        var firstId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var secondId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        record.Lines.Add(new SaleLineRecord
        {
            Id = secondId,
            SaleId = record.Id,
            ProductId = Guid.NewGuid(),
            ProductSku = "SKU-002",
            ProductName = "Producto B",
            Quantity = 1m,
            UnitPriceAmount = 5m,
            Currency = "MXN",
            Sale = record,
        });
        record.Lines.Add(new SaleLineRecord
        {
            Id = firstId,
            SaleId = record.Id,
            ProductId = Guid.NewGuid(),
            ProductSku = "SKU-001",
            ProductName = "Producto A",
            Quantity = 1m,
            UnitPriceAmount = 5m,
            Currency = "MXN",
            Sale = record,
        });
        record.Payments.Single().Amount = 10m;

        var sale = SaleMapper.ToDomain(record);

        Assert.Equal([firstId, secondId], sale.Lines.Select(l => l.Id.Value));
    }

    [Fact]
    public void ToDomainReturnsPaymentsOrderedByPaidAtUtcThenId()
    {
        var record = CreateValidDraftRecord();
        record.Lines.Single().Quantity = 100m;
        record.Payments.Clear();
        var earlierButHigherId = Guid.Parse("00000000-0000-0000-0000-000000000099");
        var laterLowerId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        record.Payments.Add(new PaymentRecord
        {
            Id = laterLowerId,
            SaleId = record.Id,
            Method = PaymentMethod.Cash,
            Amount = 100m,
            Currency = "MXN",
            PaidAtUtc = CreatedAtUtc.AddMinutes(5),
            Sale = record,
        });
        record.Payments.Add(new PaymentRecord
        {
            Id = earlierButHigherId,
            SaleId = record.Id,
            Method = PaymentMethod.Cash,
            Amount = 100m,
            Currency = "MXN",
            PaidAtUtc = CreatedAtUtc,
            Sale = record,
        });

        var sale = SaleMapper.ToDomain(record);

        Assert.Equal([earlierButHigherId, laterLowerId], sale.Payments.Select(p => p.Id.Value));
    }

    [Fact]
    public void ToDomainRejectsNullRecord()
    {
        Assert.Throws<ArgumentNullException>(() => SaleMapper.ToDomain(null!));
    }

    [Fact]
    public void ToDomainWrapsEmptySaleIdInPersistenceDataException()
    {
        var record = CreateValidDraftRecord();
        record.Id = Guid.Empty;
        record.Lines.Single().SaleId = Guid.Empty;
        record.Payments.Single().SaleId = Guid.Empty;

        Assert.Throws<PersistenceDataException>(() => SaleMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsInvalidCurrencyInPersistenceDataException()
    {
        var record = CreateValidDraftRecord();
        record.Currency = "M";

        Assert.Throws<PersistenceDataException>(() => SaleMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsUndefinedStatusInPersistenceDataException()
    {
        var record = CreateValidDraftRecord();
        record.Status = (SaleStatus)999;

        Assert.Throws<PersistenceDataException>(() => SaleMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsUndefinedPaymentMethodInPersistenceDataException()
    {
        var record = CreateValidDraftRecord();
        record.Payments.Single().Method = (PaymentMethod)999;

        Assert.Throws<PersistenceDataException>(() => SaleMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsDuplicateLineIdInPersistenceDataException()
    {
        var record = CreateValidDraftRecord();
        var duplicateId = record.Lines.Single().Id;
        record.Lines.Add(new SaleLineRecord
        {
            Id = duplicateId,
            SaleId = record.Id,
            ProductId = Guid.NewGuid(),
            ProductSku = "SKU-002",
            ProductName = "Otro producto",
            Quantity = 1m,
            UnitPriceAmount = 1m,
            Currency = "MXN",
            Sale = record,
        });

        Assert.Throws<PersistenceDataException>(() => SaleMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsDuplicateProductIdInPersistenceDataException()
    {
        var record = CreateValidDraftRecord();
        var duplicateProductId = record.Lines.Single().ProductId;
        record.Lines.Add(new SaleLineRecord
        {
            Id = Guid.NewGuid(),
            SaleId = record.Id,
            ProductId = duplicateProductId,
            ProductSku = "SKU-002",
            ProductName = "Otro producto",
            Quantity = 1m,
            UnitPriceAmount = 1m,
            Currency = "MXN",
            Sale = record,
        });

        Assert.Throws<PersistenceDataException>(() => SaleMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsDuplicatePaymentIdInPersistenceDataException()
    {
        var record = CreateValidDraftRecord();
        var duplicateId = record.Payments.Single().Id;
        record.Payments.Add(new PaymentRecord
        {
            Id = duplicateId,
            SaleId = record.Id,
            Method = PaymentMethod.Cash,
            Amount = 1m,
            Currency = "MXN",
            PaidAtUtc = CreatedAtUtc,
            Sale = record,
        });

        Assert.Throws<PersistenceDataException>(() => SaleMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsMismatchedCurrenciesInPersistenceDataException()
    {
        var record = CreateValidDraftRecord();
        record.Lines.Single().Currency = "USD";

        Assert.Throws<PersistenceDataException>(() => SaleMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsInvalidQuantityInPersistenceDataException()
    {
        var record = CreateValidDraftRecord();
        record.Lines.Single().Quantity = 0m;

        Assert.Throws<PersistenceDataException>(() => SaleMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsInvalidUnitPriceInPersistenceDataException()
    {
        var record = CreateValidDraftRecord();
        record.Lines.Single().UnitPriceAmount = -1m;

        Assert.Throws<PersistenceDataException>(() => SaleMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsInvalidPaymentAmountInPersistenceDataException()
    {
        var record = CreateValidDraftRecord();
        record.Payments.Single().Amount = 0m;

        Assert.Throws<PersistenceDataException>(() => SaleMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsDraftWithCompletedAtUtcInPersistenceDataException()
    {
        var record = CreateValidDraftRecord();
        record.CompletedAtUtc = CompletedAtUtc;

        Assert.Throws<PersistenceDataException>(() => SaleMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsCompletedWithoutCompletedAtUtcInPersistenceDataException()
    {
        var record = CreateValidCompletedRecord();
        record.CompletedAtUtc = null;

        Assert.Throws<PersistenceDataException>(() => SaleMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsCompletedWithInsufficientPaymentInPersistenceDataException()
    {
        var record = CreateValidCompletedRecord();
        record.Payments.Single().Amount = 1m;

        Assert.Throws<PersistenceDataException>(() => SaleMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsMismatchedLineSaleIdInPersistenceDataException()
    {
        var record = CreateValidDraftRecord();
        record.Lines.Single().SaleId = Guid.NewGuid();

        Assert.Throws<PersistenceDataException>(() => SaleMapper.ToDomain(record));
    }

    [Fact]
    public void ToDomainWrapsMismatchedPaymentSaleIdInPersistenceDataException()
    {
        var record = CreateValidDraftRecord();
        record.Payments.Single().SaleId = Guid.NewGuid();

        Assert.Throws<PersistenceDataException>(() => SaleMapper.ToDomain(record));
    }

    // ---------- ToRecord ----------

    [Fact]
    public void ToRecordPreservesHeader()
    {
        var sale = SaleMapper.ToDomain(CreateValidDraftRecord());

        var record = SaleMapper.ToRecord(sale);

        Assert.Equal(sale.Id.Value, record.Id);
        Assert.Equal(sale.OrganizationId.Value, record.OrganizationId);
        Assert.Equal(sale.BranchId.Value, record.BranchId);
        Assert.Equal(sale.RegisterSessionId.Value, record.RegisterSessionId);
        Assert.Equal(sale.CreatedByUserId.Value, record.CreatedByUserId);
        Assert.Equal(sale.Subtotal.Currency, record.Currency);
        Assert.Equal(sale.Status, record.Status);
        Assert.Equal(sale.CreatedAtUtc, record.CreatedAtUtc);
        Assert.Equal(sale.CompletedAtUtc, record.CompletedAtUtc);
    }

    [Fact]
    public void ToRecordPreservesLines()
    {
        var sale = SaleMapper.ToDomain(CreateValidDraftRecord());

        var record = SaleMapper.ToRecord(sale);

        var line = sale.Lines.Single();
        var lineRecord = record.Lines.Single();
        Assert.Equal(line.Id.Value, lineRecord.Id);
        Assert.Equal(record.Id, lineRecord.SaleId);
        Assert.Equal(line.ProductId.Value, lineRecord.ProductId);
        Assert.Equal(line.ProductSku.Value, lineRecord.ProductSku);
        Assert.Equal(line.ProductName, lineRecord.ProductName);
        Assert.Equal(line.Quantity, lineRecord.Quantity);
        Assert.Equal(line.UnitPrice.Amount, lineRecord.UnitPriceAmount);
        Assert.Equal(line.UnitPrice.Currency, lineRecord.Currency);
    }

    [Fact]
    public void ToRecordPreservesPayments()
    {
        var sale = SaleMapper.ToDomain(CreateValidDraftRecord());

        var record = SaleMapper.ToRecord(sale);

        var payment = sale.Payments.Single();
        var paymentRecord = record.Payments.Single();
        Assert.Equal(payment.Id.Value, paymentRecord.Id);
        Assert.Equal(record.Id, paymentRecord.SaleId);
        Assert.Equal(payment.Method, paymentRecord.Method);
        Assert.Equal(payment.Amount.Amount, paymentRecord.Amount);
        Assert.Equal(payment.Amount.Currency, paymentRecord.Currency);
        Assert.Equal(payment.PaidAtUtc, paymentRecord.PaidAtUtc);
    }

    [Fact]
    public void ToRecordDoesNotPersistDerivedValues()
    {
        var sale = SaleMapper.ToDomain(CreateValidDraftRecord());

        var record = SaleMapper.ToRecord(sale);

        Assert.DoesNotContain("Subtotal", typeof(SaleRecord).GetProperties().Select(p => p.Name));
        Assert.DoesNotContain("Total", typeof(SaleRecord).GetProperties().Select(p => p.Name));
        Assert.DoesNotContain("PaidAmount", typeof(SaleRecord).GetProperties().Select(p => p.Name));
        Assert.DoesNotContain("BalanceDue", typeof(SaleRecord).GetProperties().Select(p => p.Name));
        Assert.DoesNotContain("ChangeDue", typeof(SaleRecord).GetProperties().Select(p => p.Name));
        Assert.DoesNotContain("LineSubtotal", typeof(SaleLineRecord).GetProperties().Select(p => p.Name));
        Assert.NotNull(record);
    }

    [Fact]
    public void ToRecordRejectsNullSale()
    {
        Assert.Throws<ArgumentNullException>(() => SaleMapper.ToRecord(null!));
    }

    // ---------- UpdateRecord ----------

    [Fact]
    public void UpdateRecordChangesStatusAndCompletedAtUtc()
    {
        var record = CreateValidDraftRecord();
        record.Lines.Single().Quantity = 2m;
        record.Payments.Single().Amount = 20m;
        var sale = SaleMapper.ToDomain(record);
        sale.Complete(CompletedAtUtc);

        SaleMapper.UpdateRecord(sale, record);

        Assert.Equal(SaleStatus.Completed, record.Status);
        Assert.Equal(CompletedAtUtc, record.CompletedAtUtc);
    }

    [Fact]
    public void UpdateRecordSynchronizesModifiedLine()
    {
        var record = CreateValidDraftRecord();
        var sale = SaleMapper.ToDomain(record);
        var lineId = sale.Lines.Single().Id;
        sale.ChangeLineQuantity(lineId, 9m);

        SaleMapper.UpdateRecord(sale, record);

        Assert.Equal(9m, record.Lines.Single().Quantity);
    }

    [Fact]
    public void UpdateRecordAddsNewLine()
    {
        var record = CreateValidDraftRecord();
        var sale = SaleMapper.ToDomain(record);
        sale.AddLine(
            SaleLineId.New(), ProductId.New(), new Sku("SKU-999"), "Producto nuevo", 1m, new Money(5m, "MXN"));

        SaleMapper.UpdateRecord(sale, record);

        Assert.Equal(2, record.Lines.Count);
    }

    [Fact]
    public void UpdateRecordRemovesAbsentLine()
    {
        var record = CreateValidDraftRecord();
        var sale = SaleMapper.ToDomain(record);
        var lineId = sale.Lines.Single().Id;
        sale.RemoveLine(lineId);

        SaleMapper.UpdateRecord(sale, record);

        Assert.Empty(record.Lines);
    }

    [Fact]
    public void UpdateRecordAddsNewPayment()
    {
        var record = CreateValidDraftRecord();
        var sale = SaleMapper.ToDomain(record);
        sale.AddPayment(PaymentId.New(), PaymentMethod.Cash, new Money(5m, "MXN"), CreatedAtUtc);

        SaleMapper.UpdateRecord(sale, record);

        Assert.Equal(2, record.Payments.Count);
    }

    [Fact]
    public void UpdateRecordRemovesAbsentPayment()
    {
        var record = CreateValidDraftRecord();
        var sale = SaleMapper.ToDomain(record);
        var paymentId = sale.Payments.Single().Id;
        sale.RemovePayment(paymentId);

        SaleMapper.UpdateRecord(sale, record);

        Assert.Empty(record.Payments);
    }

    [Fact]
    public void UpdateRecordDoesNotChangeIdentity()
    {
        var record = CreateValidDraftRecord();
        var originalId = record.Id;
        var originalOrganizationId = record.OrganizationId;
        var originalBranchId = record.BranchId;
        var originalRegisterSessionId = record.RegisterSessionId;
        var originalCreatedByUserId = record.CreatedByUserId;
        var originalCreatedAtUtc = record.CreatedAtUtc;
        var sale = SaleMapper.ToDomain(record);

        SaleMapper.UpdateRecord(sale, record);

        Assert.Equal(originalId, record.Id);
        Assert.Equal(originalOrganizationId, record.OrganizationId);
        Assert.Equal(originalBranchId, record.BranchId);
        Assert.Equal(originalRegisterSessionId, record.RegisterSessionId);
        Assert.Equal(originalCreatedByUserId, record.CreatedByUserId);
        Assert.Equal(originalCreatedAtUtc, record.CreatedAtUtc);
    }

    [Fact]
    public void UpdateRecordRejectsMismatchedId()
    {
        var record = CreateValidDraftRecord();
        var sale = SaleMapper.ToDomain(record);
        record.Id = Guid.NewGuid();

        Assert.Throws<PersistenceDataException>(() => SaleMapper.UpdateRecord(sale, record));
    }

    [Fact]
    public void UpdateRecordRejectsMismatchedOrganizationId()
    {
        var record = CreateValidDraftRecord();
        var sale = SaleMapper.ToDomain(record);
        record.OrganizationId = Guid.NewGuid();

        Assert.Throws<PersistenceDataException>(() => SaleMapper.UpdateRecord(sale, record));
    }

    [Fact]
    public void UpdateRecordRejectsMismatchedBranchId()
    {
        var record = CreateValidDraftRecord();
        var sale = SaleMapper.ToDomain(record);
        record.BranchId = Guid.NewGuid();

        Assert.Throws<PersistenceDataException>(() => SaleMapper.UpdateRecord(sale, record));
    }

    [Fact]
    public void UpdateRecordRejectsMismatchedRegisterSessionId()
    {
        var record = CreateValidDraftRecord();
        var sale = SaleMapper.ToDomain(record);
        record.RegisterSessionId = Guid.NewGuid();

        Assert.Throws<PersistenceDataException>(() => SaleMapper.UpdateRecord(sale, record));
    }

    [Fact]
    public void UpdateRecordRejectsMismatchedCreatedByUserId()
    {
        var record = CreateValidDraftRecord();
        var sale = SaleMapper.ToDomain(record);
        record.CreatedByUserId = Guid.NewGuid();

        Assert.Throws<PersistenceDataException>(() => SaleMapper.UpdateRecord(sale, record));
    }

    [Fact]
    public void UpdateRecordRejectsMismatchedCreatedAtUtc()
    {
        var record = CreateValidDraftRecord();
        var sale = SaleMapper.ToDomain(record);
        record.CreatedAtUtc = CreatedAtUtc.AddDays(1);

        Assert.Throws<PersistenceDataException>(() => SaleMapper.UpdateRecord(sale, record));
    }

    [Fact]
    public void UpdateRecordRejectsMismatchedCurrency()
    {
        var record = CreateValidDraftRecord();
        var sale = SaleMapper.ToDomain(record);
        record.Currency = "USD";

        Assert.Throws<PersistenceDataException>(() => SaleMapper.UpdateRecord(sale, record));
    }

    [Fact]
    public void UpdateRecordRejectsExistingLineWithIncompatibleProductId()
    {
        var record = CreateValidDraftRecord();
        var sale = SaleMapper.ToDomain(record);
        record.Lines.Single().ProductId = Guid.NewGuid();

        Assert.Throws<PersistenceDataException>(() => SaleMapper.UpdateRecord(sale, record));
    }

    [Fact]
    public void UpdateRecordRejectsExistingLineWithIncompatibleProductSku()
    {
        var record = CreateValidDraftRecord();
        var sale = SaleMapper.ToDomain(record);
        record.Lines.Single().ProductSku = "SKU-DISTINTO";

        Assert.Throws<PersistenceDataException>(() => SaleMapper.UpdateRecord(sale, record));
    }

    [Fact]
    public void UpdateRecordRejectsExistingLineWithIncompatibleProductName()
    {
        var record = CreateValidDraftRecord();
        var sale = SaleMapper.ToDomain(record);
        record.Lines.Single().ProductName = "Nombre distinto";

        Assert.Throws<PersistenceDataException>(() => SaleMapper.UpdateRecord(sale, record));
    }

    [Fact]
    public void UpdateRecordRejectsExistingLineWithIncompatibleUnitPriceAmount()
    {
        var record = CreateValidDraftRecord();
        var sale = SaleMapper.ToDomain(record);
        record.Lines.Single().UnitPriceAmount = 999m;

        Assert.Throws<PersistenceDataException>(() => SaleMapper.UpdateRecord(sale, record));
    }

    [Fact]
    public void UpdateRecordRejectsExistingLineWithIncompatibleCurrency()
    {
        var record = CreateValidDraftRecord();
        var sale = SaleMapper.ToDomain(record);
        record.Lines.Single().Currency = "USD";

        Assert.Throws<PersistenceDataException>(() => SaleMapper.UpdateRecord(sale, record));
    }

    [Fact]
    public void UpdateRecordRejectsExistingPaymentWithIncompatibleMethod()
    {
        var record = CreateValidDraftRecord();
        var sale = SaleMapper.ToDomain(record);
        record.Payments.Single().Method = PaymentMethod.Card;

        Assert.Throws<PersistenceDataException>(() => SaleMapper.UpdateRecord(sale, record));
    }

    [Fact]
    public void UpdateRecordRejectsExistingPaymentWithIncompatibleAmount()
    {
        var record = CreateValidDraftRecord();
        var sale = SaleMapper.ToDomain(record);
        record.Payments.Single().Amount = 999m;

        Assert.Throws<PersistenceDataException>(() => SaleMapper.UpdateRecord(sale, record));
    }

    [Fact]
    public void UpdateRecordRejectsExistingPaymentWithIncompatibleCurrency()
    {
        var record = CreateValidDraftRecord();
        var sale = SaleMapper.ToDomain(record);
        record.Payments.Single().Currency = "USD";

        Assert.Throws<PersistenceDataException>(() => SaleMapper.UpdateRecord(sale, record));
    }

    [Fact]
    public void UpdateRecordRejectsExistingPaymentWithIncompatiblePaidAtUtc()
    {
        var record = CreateValidDraftRecord();
        var sale = SaleMapper.ToDomain(record);
        record.Payments.Single().PaidAtUtc = CreatedAtUtc.AddMinutes(1);

        Assert.Throws<PersistenceDataException>(() => SaleMapper.UpdateRecord(sale, record));
    }

    [Fact]
    public void UpdateRecordRejectsExistingPaymentWithIncompatibleSaleId()
    {
        var record = CreateValidDraftRecord();
        var sale = SaleMapper.ToDomain(record);
        record.Payments.Single().SaleId = Guid.NewGuid();

        Assert.Throws<PersistenceDataException>(() => SaleMapper.UpdateRecord(sale, record));
    }

    [Fact]
    public void UpdateRecordRejectsDuplicateLineIdInRecord()
    {
        var record = CreateValidDraftRecord();
        var sale = SaleMapper.ToDomain(record);
        var duplicateId = record.Lines.Single().Id;
        record.Lines.Add(new SaleLineRecord
        {
            Id = duplicateId,
            SaleId = record.Id,
            ProductId = Guid.NewGuid(),
            ProductSku = "SKU-002",
            ProductName = "Otro producto",
            Quantity = 1m,
            UnitPriceAmount = 1m,
            Currency = "MXN",
            Sale = record,
        });

        Assert.Throws<PersistenceDataException>(() => SaleMapper.UpdateRecord(sale, record));
    }

    [Fact]
    public void UpdateRecordRejectsDuplicatePaymentIdInRecord()
    {
        var record = CreateValidDraftRecord();
        var sale = SaleMapper.ToDomain(record);
        var duplicateId = record.Payments.Single().Id;
        record.Payments.Add(new PaymentRecord
        {
            Id = duplicateId,
            SaleId = record.Id,
            Method = PaymentMethod.Cash,
            Amount = 1m,
            Currency = "MXN",
            PaidAtUtc = CreatedAtUtc,
            Sale = record,
        });

        Assert.Throws<PersistenceDataException>(() => SaleMapper.UpdateRecord(sale, record));
    }

    // ---------- UpdateRecord: atomicidad ----------

    [Fact]
    public void UpdateRecordLeavesRecordUntouchedWhenLineValidationFailsEvenIfPaymentAdditionWasPending()
    {
        var record = CreateValidDraftRecord();
        var sale = SaleMapper.ToDomain(record);
        // Cambio legítimo en el dominio que, de no fallar la validación de líneas,
        // habría agregado un nuevo pago durante la sincronización.
        sale.AddPayment(PaymentId.New(), PaymentMethod.Cash, new Money(5m, "MXN"), CreatedAtUtc);

        var originalStatus = record.Status;
        var originalCompletedAtUtc = record.CompletedAtUtc;
        var originalLineCount = record.Lines.Count;
        var originalPaymentCount = record.Payments.Count;
        var originalLineQuantity = record.Lines.Single().Quantity;
        var linesReference = record.Lines;
        var paymentsReference = record.Payments;

        // Incompatibilidad detectada tarde: la línea existente en record ya no coincide
        // con el snapshot inmutable del dominio.
        record.Lines.Single().ProductSku = "SKU-CORRUPTA";

        Assert.Throws<PersistenceDataException>(() => SaleMapper.UpdateRecord(sale, record));

        Assert.Equal(originalStatus, record.Status);
        Assert.Equal(originalCompletedAtUtc, record.CompletedAtUtc);
        Assert.Equal(originalLineCount, record.Lines.Count);
        Assert.Equal(originalPaymentCount, record.Payments.Count);
        Assert.Equal(originalLineQuantity, record.Lines.Single().Quantity);
        Assert.Same(linesReference, record.Lines);
        Assert.Same(paymentsReference, record.Payments);
    }

    [Fact]
    public void UpdateRecordLeavesRecordUntouchedWhenPaymentValidationFailsEvenIfLineChangeWasPending()
    {
        var record = CreateValidDraftRecord();
        var sale = SaleMapper.ToDomain(record);
        var lineId = sale.Lines.Single().Id;
        // Cambio legítimo en el dominio que, de no fallar la validación de pagos, habría
        // actualizado Quantity durante la sincronización.
        sale.ChangeLineQuantity(lineId, 9m);

        var originalStatus = record.Status;
        var originalCompletedAtUtc = record.CompletedAtUtc;
        var originalLineCount = record.Lines.Count;
        var originalPaymentCount = record.Payments.Count;
        var originalLineQuantity = record.Lines.Single().Quantity;
        var originalPaymentAmount = record.Payments.Single().Amount;
        var linesReference = record.Lines;
        var paymentsReference = record.Payments;

        // Incompatibilidad detectada tarde: el pago existente en record ya no coincide
        // con el snapshot inmutable del dominio.
        record.Payments.Single().Method = PaymentMethod.Card;

        Assert.Throws<PersistenceDataException>(() => SaleMapper.UpdateRecord(sale, record));

        Assert.Equal(originalStatus, record.Status);
        Assert.Equal(originalCompletedAtUtc, record.CompletedAtUtc);
        Assert.Equal(originalLineCount, record.Lines.Count);
        Assert.Equal(originalPaymentCount, record.Payments.Count);
        Assert.Equal(originalLineQuantity, record.Lines.Single().Quantity);
        Assert.Equal(originalPaymentAmount, record.Payments.Single().Amount);
        Assert.Same(linesReference, record.Lines);
        Assert.Same(paymentsReference, record.Payments);
    }

    [Fact]
    public void UpdateRecordLeavesRecordDraftWhenCompletingSaleAndExistingPaymentIsIncompatible()
    {
        var record = CreateValidDraftRecord();
        var sale = SaleMapper.ToDomain(record);
        sale.Complete(CompletedAtUtc);

        // Incompatibilidad detectada tarde: el pago existente en record ya no coincide
        // con el snapshot inmutable del dominio que se intenta completar.
        record.Payments.Single().Amount = 999m;

        Assert.Throws<PersistenceDataException>(() => SaleMapper.UpdateRecord(sale, record));

        Assert.Equal(SaleStatus.Draft, record.Status);
        Assert.Null(record.CompletedAtUtc);
    }

    [Fact]
    public void UpdateRecordRejectsNullSale()
    {
        var record = CreateValidDraftRecord();

        Assert.Throws<ArgumentNullException>(() => SaleMapper.UpdateRecord(null!, record));
    }

    [Fact]
    public void UpdateRecordRejectsNullRecord()
    {
        var sale = SaleMapper.ToDomain(CreateValidDraftRecord());

        Assert.Throws<ArgumentNullException>(() => SaleMapper.UpdateRecord(sale, null!));
    }
}
