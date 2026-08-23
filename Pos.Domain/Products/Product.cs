using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Common.ValueObjects;

namespace Pos.Domain.Products;

public sealed class Product
{
    public ProductId Id { get; }

    public OrganizationId OrganizationId { get; }

    public Sku Sku { get; private set; }

    public Barcode? Barcode { get; private set; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public Money SalePrice { get; private set; }

    public Money? Cost { get; private set; }

    public bool TracksInventory { get; private set; }

    public bool IsActive { get; private set; }

    // Fotografía opcional del producto (BASIC-UX-01, sección 5): solo el nombre de archivo
    // administrado por PosPlatform bajo ApplicationPathProvider.ProductImagesDirectory, nunca una
    // ruta absoluta de máquina. null significa "sin foto", un estado válido y normal.
    public string? ImageFileName { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; }

    public Product(
        ProductId id,
        OrganizationId organizationId,
        Sku sku,
        Barcode? barcode,
        string name,
        string? description,
        Money salePrice,
        Money? cost,
        bool tracksInventory,
        DateTimeOffset createdAtUtc)
        : this(id, organizationId, sku, barcode, name, description, salePrice, cost, tracksInventory, true, null, createdAtUtc)
    {
    }

    private Product(
        ProductId id,
        OrganizationId organizationId,
        Sku sku,
        Barcode? barcode,
        string name,
        string? description,
        Money salePrice,
        Money? cost,
        bool tracksInventory,
        bool isActive,
        string? imageFileName,
        DateTimeOffset createdAtUtc)
    {
        var validSalePrice = EnsureSalePrice(salePrice);
        var validCost = EnsureCost(cost);
        EnsureSameCurrency(validSalePrice, validCost);

        Id = EnsureNotEmpty(id);
        OrganizationId = EnsureNotEmpty(organizationId);
        Sku = sku;
        Barcode = barcode;
        Name = NormalizeName(name);
        Description = NormalizeDescription(description);
        SalePrice = validSalePrice;
        Cost = validCost;
        TracksInventory = tracksInventory;
        CreatedAtUtc = EnsureUtc(createdAtUtc);
        IsActive = isActive;
        ImageFileName = NormalizeImageFileName(imageFileName);
    }

    // Reconstruye estado ya persistido, incluyendo IsActive/ImageFileName, sin pasar por
    // Activate/Deactivate/ChangeImage.
    public static Product Rehydrate(
        ProductId id,
        OrganizationId organizationId,
        Sku sku,
        Barcode? barcode,
        string name,
        string? description,
        Money salePrice,
        Money? cost,
        bool tracksInventory,
        bool isActive,
        string? imageFileName,
        DateTimeOffset createdAtUtc) =>
        new(id, organizationId, sku, barcode, name, description, salePrice, cost, tracksInventory, isActive, imageFileName, createdAtUtc);

    public void Rename(string name)
    {
        Name = NormalizeName(name);
    }

    public void ChangeDescription(string? description)
    {
        Description = NormalizeDescription(description);
    }

    public void ChangeSku(Sku sku)
    {
        Sku = sku;
    }

    public void ChangeBarcode(Barcode? barcode)
    {
        Barcode = barcode;
    }

    public void ChangeSalePrice(Money salePrice)
    {
        var validSalePrice = EnsureSalePrice(salePrice);
        EnsureSameCurrency(validSalePrice, Cost);

        SalePrice = validSalePrice;
    }

    public void ChangeCost(Money? cost)
    {
        var validCost = EnsureCost(cost);
        EnsureSameCurrency(SalePrice, validCost);

        Cost = validCost;
    }

    public void EnableInventoryTracking()
    {
        TracksInventory = true;
    }

    public void DisableInventoryTracking()
    {
        TracksInventory = false;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    // Fija/reemplaza la foto del producto con un nombre de archivo administrado ya generado por
    // IProductImageStore (nunca una ruta absoluta ni el nombre original del archivo elegido por el
    // usuario). Reemplazar una foto existente simplemente sobrescribe la referencia: la limpieza
    // del archivo administrado anterior es responsabilidad de la capa de aplicación, después de
    // persistir este cambio (BASIC-UX-01, sección 13).
    public void ChangeImage(string imageFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imageFileName);
        ImageFileName = imageFileName;
    }

    public void RemoveImage()
    {
        ImageFileName = null;
    }

    private static ProductId EnsureNotEmpty(ProductId id)
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

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("Name es obligatorio.");
        }

        var trimmed = name.Trim();

        if (trimmed.Length is < 2 or > 160)
        {
            throw new DomainValidationException("Name debe tener entre 2 y 160 caracteres.");
        }

        return trimmed;
    }

    private static string? NormalizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var trimmed = description.Trim();

        if (trimmed.Length > 500)
        {
            throw new DomainValidationException("Description no puede exceder 500 caracteres.");
        }

        return trimmed;
    }

    private static string? NormalizeImageFileName(string? imageFileName) =>
        string.IsNullOrWhiteSpace(imageFileName) ? null : imageFileName;

    private static Money EnsureSalePrice(Money salePrice)
    {
        if (salePrice is null)
        {
            throw new DomainValidationException("SalePrice es obligatorio.");
        }

        if (salePrice.Amount < 0m)
        {
            throw new DomainValidationException("SalePrice no puede ser negativo.");
        }

        return salePrice;
    }

    private static Money? EnsureCost(Money? cost)
    {
        if (cost is null)
        {
            return null;
        }

        if (cost.Amount < 0m)
        {
            throw new DomainValidationException("Cost no puede ser negativo.");
        }

        return cost;
    }

    private static void EnsureSameCurrency(Money salePrice, Money? cost)
    {
        if (cost is not null && cost.Currency != salePrice.Currency)
        {
            throw new DomainValidationException("Cost.Currency debe coincidir con SalePrice.Currency.");
        }
    }

    private static DateTimeOffset EnsureUtc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new DomainValidationException("CreatedAtUtc debe tener Offset igual a TimeSpan.Zero.");
        }

        return value;
    }
}
