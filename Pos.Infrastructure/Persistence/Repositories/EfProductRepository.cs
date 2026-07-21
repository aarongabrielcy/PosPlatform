using Microsoft.EntityFrameworkCore;
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

    public async Task AddAsync(Product product, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(product);

        var record = ProductMapper.ToRecord(product);

        await _context.Products.AddAsync(record, cancellationToken);
    }
}
