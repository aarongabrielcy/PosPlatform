using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Exceptions;
using Pos.Application.Products;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Products;
using Pos.Infrastructure.Persistence.Mappers;

namespace Pos.Infrastructure.Persistence.Repositories;

public sealed class EfProductRepository : IProductRepository
{
    private readonly PosDbContext _context;

    public EfProductRepository(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Product?> GetByIdAsync(ProductId productId, CancellationToken cancellationToken)
    {
        var record = await _context.Products
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == productId.Value, cancellationToken);

        return record is null ? null : ProductMapper.ToDomain(record);
    }

    public async Task<Product?> GetBySkuAsync(OrganizationId organizationId, Sku sku, CancellationToken cancellationToken)
    {
        var skuValue = sku.Value;

        var record = await _context.Products
            .AsNoTracking()
            .SingleOrDefaultAsync(
                r => r.OrganizationId == organizationId.Value && r.Sku == skuValue,
                cancellationToken);

        return record is null ? null : ProductMapper.ToDomain(record);
    }

    public async Task<Product?> GetByBarcodeAsync(OrganizationId organizationId, Barcode barcode, CancellationToken cancellationToken)
    {
        var barcodeValue = barcode.Value;

        var record = await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.OrganizationId == organizationId.Value && r.Barcode == barcodeValue,
                cancellationToken);

        return record is null ? null : ProductMapper.ToDomain(record);
    }

    public async Task<IReadOnlyList<Product>> GetByOrganizationAsync(
        OrganizationId organizationId, CancellationToken cancellationToken)
    {
        var records = await _context.Products
            .AsNoTracking()
            .Where(r => r.OrganizationId == organizationId.Value)
            .OrderBy(r => r.Name)
            .ThenBy(r => r.Id)
            .ToListAsync(cancellationToken);

        return records.Select(ProductMapper.ToDomain).ToList();
    }

    public async Task<IReadOnlyList<Product>> SearchActiveAsync(
        OrganizationId organizationId, string searchTerm, int maxResults, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(searchTerm);

        if (maxResults <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxResults), maxResults, "maxResults debe ser mayor que cero.");
        }

        var term = searchTerm.Trim();
        var upperTerm = term.ToUpperInvariant();

        // La coincidencia exacta de Sku/Barcode se prioriza (rango 0), luego el nombre que
        // comienza con el término (rango 1) y por último cualquier otra coincidencia parcial de
        // Sku/Barcode/Name (rango 2). Sku ya se almacena normalizado en mayúsculas.
        var records = await _context.Products
            .AsNoTracking()
            .Where(r => r.OrganizationId == organizationId.Value && r.IsActive)
            .Where(r =>
                r.Sku.Contains(upperTerm) ||
                (r.Barcode != null && r.Barcode.Contains(term)) ||
                EF.Functions.Like(r.Name, $"%{term}%"))
            .OrderBy(r =>
                r.Sku == upperTerm || r.Barcode == term
                    ? 0
                    : EF.Functions.Like(r.Name, $"{term}%")
                        ? 1
                        : 2)
            .ThenBy(r => r.Name)
            .ThenBy(r => r.Id)
            .Take(maxResults)
            .ToListAsync(cancellationToken);

        return records.Select(ProductMapper.ToDomain).ToList();
    }

    public async Task<IReadOnlyList<Product>> SearchAsync(
        OrganizationId organizationId, string searchTerm, bool includeInactive, int maxResults, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(searchTerm);

        if (maxResults <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxResults), maxResults, "maxResults debe ser mayor que cero.");
        }

        var term = searchTerm.Trim();
        var upperTerm = term.ToUpperInvariant();

        // Mismo criterio de orden que SearchActiveAsync, solo difiere en el filtro de IsActive.
        var records = await _context.Products
            .AsNoTracking()
            .Where(r => r.OrganizationId == organizationId.Value && (includeInactive || r.IsActive))
            .Where(r =>
                r.Sku.Contains(upperTerm) ||
                (r.Barcode != null && r.Barcode.Contains(term)) ||
                EF.Functions.Like(r.Name, $"%{term}%"))
            .OrderBy(r =>
                r.Sku == upperTerm || r.Barcode == term
                    ? 0
                    : EF.Functions.Like(r.Name, $"{term}%")
                        ? 1
                        : 2)
            .ThenBy(r => r.Name)
            .ThenBy(r => r.Id)
            .Take(maxResults)
            .ToListAsync(cancellationToken);

        return records.Select(ProductMapper.ToDomain).ToList();
    }

    public async Task AddAsync(Product product, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(product);

        var record = ProductMapper.ToRecord(product);

        await _context.Products.AddAsync(record, cancellationToken);
    }

    public async Task UpdateAsync(Product product, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(product);

        var record = await _context.Products
            .SingleOrDefaultAsync(r => r.Id == product.Id.Value, cancellationToken);

        if (record is null)
        {
            throw new EntityNotFoundException("Product", product.Id.ToString());
        }

        ProductMapper.UpdateRecord(product, record);
    }
}
