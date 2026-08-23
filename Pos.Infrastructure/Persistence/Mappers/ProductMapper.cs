using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Common.ValueObjects;
using Pos.Domain.Products;
using Pos.Infrastructure.Persistence.Exceptions;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Persistence.Mappers;

internal static class ProductMapper
{
    internal static Product ToDomain(ProductRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        try
        {
            var sku = new Sku(record.Sku);
            Barcode? barcode = record.Barcode is null ? null : new Barcode(record.Barcode);
            var salePrice = new Money(record.SalePriceAmount, record.SalePriceCurrency);
            var cost = ToCost(record);

            return Product.Rehydrate(
                new ProductId(record.Id),
                new OrganizationId(record.OrganizationId),
                sku,
                barcode,
                record.Name,
                record.Description,
                salePrice,
                cost,
                record.TracksInventory,
                record.IsActive,
                record.ImageFileName,
                record.CreatedAtUtc);
        }
        catch (DomainValidationException ex)
        {
            throw new PersistenceDataException(
                $"ProductRecord con Id '{record.Id}' contiene datos inválidos: {ex.Message}", ex);
        }
    }

    internal static ProductRecord ToRecord(Product product)
    {
        ArgumentNullException.ThrowIfNull(product);

        return new ProductRecord
        {
            Id = product.Id.Value,
            OrganizationId = product.OrganizationId.Value,
            Sku = product.Sku.Value,
            Barcode = product.Barcode?.Value,
            Name = product.Name,
            Description = product.Description,
            SalePriceAmount = product.SalePrice.Amount,
            SalePriceCurrency = product.SalePrice.Currency,
            CostAmount = product.Cost?.Amount,
            CostCurrency = product.Cost?.Currency,
            TracksInventory = product.TracksInventory,
            IsActive = product.IsActive,
            ImageFileName = product.ImageFileName,
            CreatedAtUtc = product.CreatedAtUtc,
        };
    }

    internal static void UpdateRecord(Product product, ProductRecord record)
    {
        ArgumentNullException.ThrowIfNull(product);
        ArgumentNullException.ThrowIfNull(record);

        if (record.Id != product.Id.Value)
        {
            throw new PersistenceDataException(
                $"No se puede actualizar ProductRecord '{record.Id}': Id no coincide con Product '{product.Id}'.");
        }

        if (record.OrganizationId != product.OrganizationId.Value)
        {
            throw new PersistenceDataException(
                $"No se puede actualizar ProductRecord '{record.Id}': OrganizationId no coincide.");
        }

        if (record.CreatedAtUtc != product.CreatedAtUtc)
        {
            throw new PersistenceDataException(
                $"No se puede actualizar ProductRecord '{record.Id}': CreatedAtUtc no coincide.");
        }

        record.Sku = product.Sku.Value;
        record.Barcode = product.Barcode?.Value;
        record.Name = product.Name;
        record.Description = product.Description;
        record.SalePriceAmount = product.SalePrice.Amount;
        record.SalePriceCurrency = product.SalePrice.Currency;
        record.CostAmount = product.Cost?.Amount;
        record.CostCurrency = product.Cost?.Currency;
        record.TracksInventory = product.TracksInventory;
        record.IsActive = product.IsActive;
        record.ImageFileName = product.ImageFileName;
    }

    private static Money? ToCost(ProductRecord record)
    {
        if (record.CostAmount is null && record.CostCurrency is null)
        {
            return null;
        }

        if (record.CostAmount is null || record.CostCurrency is null)
        {
            throw new PersistenceDataException(
                $"ProductRecord con Id '{record.Id}' contiene datos inválidos: " +
                "CostAmount y CostCurrency deben estar ambos presentes o ambos ausentes.");
        }

        return new Money(record.CostAmount.Value, record.CostCurrency);
    }
}
