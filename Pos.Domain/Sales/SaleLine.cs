using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Common.ValueObjects;
using Pos.Domain.Products;

namespace Pos.Domain.Sales;

public sealed class SaleLine
{
    public SaleLineId Id { get; }

    public ProductId ProductId { get; }

    public Sku ProductSku { get; }

    public string ProductName { get; }

    public decimal Quantity { get; private set; }

    public Money UnitPrice { get; }

    public Money LineSubtotal { get; private set; }

    public SaleLine(
        SaleLineId id,
        ProductId productId,
        Sku productSku,
        string productName,
        decimal quantity,
        Money unitPrice)
    {
        Id = EnsureNotEmpty(id);
        ProductId = EnsureNotEmpty(productId);
        ProductSku = EnsureValidSku(productSku);
        ProductName = NormalizeName(productName);
        Quantity = EnsurePositive(quantity, nameof(quantity));
        UnitPrice = EnsureNonNegative(unitPrice, nameof(unitPrice));
        LineSubtotal = UnitPrice * Quantity;
    }

    public void ChangeQuantity(decimal quantity)
    {
        var validQuantity = EnsurePositive(quantity, nameof(quantity));

        Quantity = validQuantity;
        LineSubtotal = UnitPrice * Quantity;
    }

    internal SaleLine CreateSnapshot()
    {
        return new SaleLine(Id, ProductId, ProductSku, ProductName, Quantity, UnitPrice);
    }

    private static SaleLineId EnsureNotEmpty(SaleLineId id)
    {
        if (id.Value == Guid.Empty)
        {
            throw new DomainValidationException("Id no puede ser vacío.");
        }

        return id;
    }

    private static ProductId EnsureNotEmpty(ProductId productId)
    {
        if (productId.Value == Guid.Empty)
        {
            throw new DomainValidationException("ProductId no puede ser vacío.");
        }

        return productId;
    }

    private static Sku EnsureValidSku(Sku productSku)
    {
        if (string.IsNullOrWhiteSpace(productSku.Value))
        {
            throw new DomainValidationException("ProductSku no puede ser vacío.");
        }

        return productSku;
    }

    private static string NormalizeName(string productName)
    {
        if (string.IsNullOrWhiteSpace(productName))
        {
            throw new DomainValidationException("ProductName es obligatorio.");
        }

        var trimmed = productName.Trim();

        if (trimmed.Length is < 2 or > 160)
        {
            throw new DomainValidationException("ProductName debe tener entre 2 y 160 caracteres.");
        }

        return trimmed;
    }

    private static decimal EnsurePositive(decimal value, string parameterName)
    {
        if (value <= 0m)
        {
            throw new DomainValidationException($"{parameterName} debe ser mayor que cero.");
        }

        return value;
    }

    private static Money EnsureNonNegative(Money money, string parameterName)
    {
        if (money is null)
        {
            throw new DomainValidationException($"{parameterName} es obligatorio.");
        }

        if (money.Amount < 0m)
        {
            throw new DomainValidationException($"{parameterName} no puede ser negativo.");
        }

        return money;
    }
}
