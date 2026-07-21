using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Common.ValueObjects;
using Pos.Domain.Products;

namespace Pos.Domain.Sales;

public sealed class Sale
{
    private readonly List<SaleLine> _lines = new();

    private readonly List<Payment> _payments = new();

    private readonly string _currency;

    public SaleId Id { get; }

    public OrganizationId OrganizationId { get; }

    public BranchId BranchId { get; }

    public RegisterSessionId RegisterSessionId { get; }

    public UserId CreatedByUserId { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public IReadOnlyCollection<SaleLine> Lines => _lines.Select(line => line.CreateSnapshot()).ToList();

    public Money Subtotal { get; private set; }

    public Money Total { get; private set; }

    public SaleStatus Status { get; private set; }

    public IReadOnlyCollection<Payment> Payments => _payments.ToList();

    public Money PaidAmount { get; private set; }

    public Money BalanceDue { get; private set; }

    public Money ChangeDue { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public Sale(
        SaleId id,
        OrganizationId organizationId,
        BranchId branchId,
        RegisterSessionId registerSessionId,
        UserId createdByUserId,
        string currency,
        DateTimeOffset createdAtUtc)
    {
        Id = EnsureNotEmpty(id);
        OrganizationId = EnsureNotEmpty(organizationId);
        BranchId = EnsureNotEmpty(branchId);
        RegisterSessionId = EnsureNotEmpty(registerSessionId);
        CreatedByUserId = EnsureNotEmpty(createdByUserId);
        CreatedAtUtc = EnsureUtc(createdAtUtc, nameof(createdAtUtc));

        var zero = Money.Zero(currency);
        _currency = zero.Currency;
        Subtotal = zero;
        Total = zero;
        Status = SaleStatus.Draft;
        PaidAmount = zero;
        BalanceDue = zero;
        ChangeDue = zero;
    }

    public void AddLine(
        SaleLineId saleLineId,
        ProductId productId,
        Sku productSku,
        string productName,
        decimal quantity,
        Money unitPrice)
    {
        EnsureDraft();

        var newLine = new SaleLine(saleLineId, productId, productSku, productName, quantity, unitPrice);

        if (unitPrice.Currency != _currency)
        {
            throw new DomainValidationException("UnitPrice.Currency debe coincidir con la moneda de la venta.");
        }

        if (_lines.Any(line => line.Id == newLine.Id))
        {
            throw new DomainValidationException("Ya existe una línea con el mismo SaleLineId.");
        }

        if (_lines.Any(line => line.ProductId == newLine.ProductId))
        {
            throw new DomainValidationException("Ya existe una línea con el mismo ProductId.");
        }

        _lines.Add(newLine);
        RecalculateTotals();
    }

    public void ChangeLineQuantity(SaleLineId saleLineId, decimal quantity)
    {
        EnsureDraft();

        var line = FindLineOrThrow(saleLineId);

        var projectedLine = line.CreateSnapshot();
        projectedLine.ChangeQuantity(quantity);

        var projectedTotal = Total - line.LineSubtotal + projectedLine.LineSubtotal;
        EnsureNonCashPaymentsDoNotExceed(projectedTotal);

        line.ChangeQuantity(quantity);
        RecalculateTotals();
    }

    public void RemoveLine(SaleLineId saleLineId)
    {
        EnsureDraft();

        var line = FindLineOrThrow(saleLineId);

        var projectedTotal = Total - line.LineSubtotal;
        EnsureNonCashPaymentsDoNotExceed(projectedTotal);

        _lines.Remove(line);
        RecalculateTotals();
    }

    public void AddPayment(
        PaymentId paymentId,
        PaymentMethod method,
        Money amount,
        DateTimeOffset paidAtUtc)
    {
        EnsureDraft();

        var payment = new Payment(paymentId, method, amount, paidAtUtc);

        if (payment.Amount.Currency != _currency)
        {
            throw new DomainValidationException("Amount.Currency debe coincidir con la moneda de la venta.");
        }

        if (_payments.Any(p => p.Id == payment.Id))
        {
            throw new DomainValidationException("Ya existe un pago con el mismo PaymentId.");
        }

        var projectedPaidAmount = PaidAmount + payment.Amount;

        if (payment.Method != PaymentMethod.Cash && projectedPaidAmount.Amount > Total.Amount)
        {
            throw new DomainValidationException(
                "Un pago Card o BankTransfer no puede provocar que PaidAmount supere Total.");
        }

        _payments.Add(payment);
        RecalculatePaymentAmounts();
    }

    public void RemovePayment(PaymentId paymentId)
    {
        EnsureDraft();

        if (paymentId.Value == Guid.Empty)
        {
            throw new DomainValidationException("PaymentId no puede ser vacío.");
        }

        var payment = _payments.FirstOrDefault(p => p.Id == paymentId);

        if (payment is null)
        {
            throw new DomainValidationException("No existe un pago con el PaymentId indicado.");
        }

        _payments.Remove(payment);
        RecalculatePaymentAmounts();
    }

    public void EnsureCanComplete(DateTimeOffset completedAtUtc)
    {
        EnsureDraft();

        if (_lines.Count == 0)
        {
            throw new DomainValidationException("No se puede completar una venta sin líneas.");
        }

        if (Total.Amount <= 0m)
        {
            throw new DomainValidationException("Total debe ser mayor que cero para completar la venta.");
        }

        if (PaidAmount.Amount < Total.Amount)
        {
            throw new DomainValidationException("PaidAmount debe ser igual o mayor que Total para completar la venta.");
        }

        if (BalanceDue.Amount != 0m)
        {
            throw new DomainValidationException("BalanceDue debe ser cero para completar la venta.");
        }

        var validCompletedAtUtc = EnsureUtc(completedAtUtc, nameof(completedAtUtc));

        if (validCompletedAtUtc < CreatedAtUtc)
        {
            throw new DomainValidationException("completedAtUtc no puede ser anterior a CreatedAtUtc.");
        }
    }

    public void Complete(DateTimeOffset completedAtUtc)
    {
        EnsureCanComplete(completedAtUtc);

        Status = SaleStatus.Completed;
        CompletedAtUtc = EnsureUtc(completedAtUtc, nameof(completedAtUtc));
    }

    // Reconstruye una venta ya persistida. A diferencia del constructor, acepta líneas y pagos
    // existentes y puede reconstruir directamente en estado Completed.
    public static Sale Rehydrate(
        SaleId id,
        OrganizationId organizationId,
        BranchId branchId,
        RegisterSessionId registerSessionId,
        UserId createdByUserId,
        string currency,
        DateTimeOffset createdAtUtc,
        IReadOnlyCollection<SaleLine> lines,
        IReadOnlyCollection<Payment> payments,
        SaleStatus status,
        DateTimeOffset? completedAtUtc) =>
        new(
            id,
            organizationId,
            branchId,
            registerSessionId,
            createdByUserId,
            currency,
            createdAtUtc,
            lines,
            payments,
            status,
            completedAtUtc);

    private Sale(
        SaleId id,
        OrganizationId organizationId,
        BranchId branchId,
        RegisterSessionId registerSessionId,
        UserId createdByUserId,
        string currency,
        DateTimeOffset createdAtUtc,
        IReadOnlyCollection<SaleLine> lines,
        IReadOnlyCollection<Payment> payments,
        SaleStatus status,
        DateTimeOffset? completedAtUtc)
    {
        Id = EnsureNotEmpty(id);
        OrganizationId = EnsureNotEmpty(organizationId);
        BranchId = EnsureNotEmpty(branchId);
        RegisterSessionId = EnsureNotEmpty(registerSessionId);
        CreatedByUserId = EnsureNotEmpty(createdByUserId);
        CreatedAtUtc = EnsureUtc(createdAtUtc, nameof(createdAtUtc));

        var zero = Money.Zero(currency);
        _currency = zero.Currency;
        Subtotal = zero;
        Total = zero;
        PaidAmount = zero;
        BalanceDue = zero;
        ChangeDue = zero;

        if (lines is null)
        {
            throw new DomainValidationException("lines no puede ser nulo.");
        }

        if (payments is null)
        {
            throw new DomainValidationException("payments no puede ser nulo.");
        }

        foreach (var line in lines)
        {
            if (line is null)
            {
                throw new DomainValidationException("Las líneas no pueden contener elementos nulos.");
            }

            if (line.UnitPrice.Currency != _currency)
            {
                throw new DomainValidationException("UnitPrice.Currency debe coincidir con la moneda de la venta.");
            }

            if (_lines.Any(l => l.Id == line.Id))
            {
                throw new DomainValidationException("Ya existe una línea con el mismo SaleLineId.");
            }

            if (_lines.Any(l => l.ProductId == line.ProductId))
            {
                throw new DomainValidationException("Ya existe una línea con el mismo ProductId.");
            }

            _lines.Add(line.CreateSnapshot());
        }

        foreach (var payment in payments)
        {
            if (payment is null)
            {
                throw new DomainValidationException("Los pagos no pueden contener elementos nulos.");
            }

            if (payment.Amount.Currency != _currency)
            {
                throw new DomainValidationException("Amount.Currency debe coincidir con la moneda de la venta.");
            }

            if (_payments.Any(p => p.Id == payment.Id))
            {
                throw new DomainValidationException("Ya existe un pago con el mismo PaymentId.");
            }

            _payments.Add(new Payment(payment.Id, payment.Method, payment.Amount, payment.PaidAtUtc));
        }

        RecalculateTotals();

        var nonCashPaidAmount = CalculateNonCashPaidAmount();

        if (nonCashPaidAmount.Amount > Total.Amount)
        {
            throw new DomainValidationException(
                "Los pagos Card o BankTransfer no pueden superar el Total de la venta.");
        }

        switch (status)
        {
            case SaleStatus.Draft:
                if (completedAtUtc is not null)
                {
                    throw new DomainValidationException("Una venta Draft no puede tener CompletedAtUtc.");
                }

                Status = SaleStatus.Draft;
                CompletedAtUtc = null;
                break;

            case SaleStatus.Completed:
                if (completedAtUtc is null)
                {
                    throw new DomainValidationException("Una venta Completed requiere CompletedAtUtc.");
                }

                var validCompletedAtUtc = EnsureUtc(completedAtUtc.Value, nameof(completedAtUtc));
                EnsureCanComplete(validCompletedAtUtc);

                Status = SaleStatus.Completed;
                CompletedAtUtc = validCompletedAtUtc;
                break;

            default:
                throw new DomainValidationException("status no es un valor válido de SaleStatus.");
        }
    }

    public bool ContainsProduct(ProductId productId)
    {
        if (productId.Value == Guid.Empty)
        {
            throw new DomainValidationException("ProductId no puede ser vacío.");
        }

        return _lines.Any(line => line.ProductId == productId);
    }

    public SaleLine GetLine(SaleLineId saleLineId)
    {
        return FindLineOrThrow(saleLineId).CreateSnapshot();
    }

    private SaleLine FindLineOrThrow(SaleLineId saleLineId)
    {
        if (saleLineId.Value == Guid.Empty)
        {
            throw new DomainValidationException("SaleLineId no puede ser vacío.");
        }

        var line = _lines.FirstOrDefault(l => l.Id == saleLineId);

        if (line is null)
        {
            throw new DomainValidationException("No existe una línea con el SaleLineId indicado.");
        }

        return line;
    }

    private void EnsureDraft()
    {
        if (Status != SaleStatus.Draft)
        {
            throw new DomainValidationException("La operación solo es válida para una venta en estado Draft.");
        }
    }

    private void RecalculateTotals()
    {
        var subtotal = Money.Zero(_currency);

        foreach (var line in _lines)
        {
            subtotal += line.LineSubtotal;
        }

        Subtotal = subtotal;
        Total = subtotal;

        RecalculatePaymentAmounts();
    }

    private void RecalculatePaymentAmounts()
    {
        var paidAmount = Money.Zero(_currency);

        foreach (var payment in _payments)
        {
            paidAmount += payment.Amount;
        }

        PaidAmount = paidAmount;
        BalanceDue = paidAmount.Amount >= Total.Amount ? Money.Zero(_currency) : Total - paidAmount;
        ChangeDue = paidAmount.Amount > Total.Amount ? paidAmount - Total : Money.Zero(_currency);
    }

    private Money CalculateNonCashPaidAmount()
    {
        var nonCashPaidAmount = Money.Zero(_currency);

        foreach (var payment in _payments)
        {
            if (payment.Method != PaymentMethod.Cash)
            {
                nonCashPaidAmount += payment.Amount;
            }
        }

        return nonCashPaidAmount;
    }

    private void EnsureNonCashPaymentsDoNotExceed(Money projectedTotal)
    {
        var nonCashPaidAmount = CalculateNonCashPaidAmount();

        if (nonCashPaidAmount.Amount > projectedTotal.Amount)
        {
            throw new DomainValidationException(
                "Los pagos Card o BankTransfer no pueden superar el Total proyectado de la venta.");
        }
    }

    private static SaleId EnsureNotEmpty(SaleId id)
    {
        if (id.Value == Guid.Empty)
        {
            throw new DomainValidationException("Id no puede ser vacío.");
        }

        return id;
    }

    private static OrganizationId EnsureNotEmpty(OrganizationId organizationId)
    {
        if (organizationId.Value == Guid.Empty)
        {
            throw new DomainValidationException("OrganizationId no puede ser vacío.");
        }

        return organizationId;
    }

    private static BranchId EnsureNotEmpty(BranchId branchId)
    {
        if (branchId.Value == Guid.Empty)
        {
            throw new DomainValidationException("BranchId no puede ser vacío.");
        }

        return branchId;
    }

    private static RegisterSessionId EnsureNotEmpty(RegisterSessionId registerSessionId)
    {
        if (registerSessionId.Value == Guid.Empty)
        {
            throw new DomainValidationException("RegisterSessionId no puede ser vacío.");
        }

        return registerSessionId;
    }

    private static UserId EnsureNotEmpty(UserId userId)
    {
        if (userId.Value == Guid.Empty)
        {
            throw new DomainValidationException("UserId no puede ser vacío.");
        }

        return userId;
    }

    private static DateTimeOffset EnsureUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new DomainValidationException($"{parameterName} debe tener Offset igual a TimeSpan.Zero.");
        }

        return value;
    }
}
