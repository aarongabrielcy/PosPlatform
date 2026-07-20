using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Common.ValueObjects;
using Pos.Domain.Products;

namespace Pos.Domain.Sales;

public sealed class Sale
{
    private readonly List<SaleLine> _lines = new();

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
    }

    public void AddLine(
        SaleLineId saleLineId,
        ProductId productId,
        Sku productSku,
        string productName,
        decimal quantity,
        Money unitPrice)
    {
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
        var line = FindLineOrThrow(saleLineId);

        line.ChangeQuantity(quantity);
        RecalculateTotals();
    }

    public void RemoveLine(SaleLineId saleLineId)
    {
        var line = FindLineOrThrow(saleLineId);

        _lines.Remove(line);
        RecalculateTotals();
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

    private void RecalculateTotals()
    {
        var subtotal = Money.Zero(_currency);

        foreach (var line in _lines)
        {
            subtotal += line.LineSubtotal;
        }

        Subtotal = subtotal;
        Total = subtotal;
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
