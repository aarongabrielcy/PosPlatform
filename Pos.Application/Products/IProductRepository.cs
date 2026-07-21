using Pos.Domain.Common.Identifiers;
using Pos.Domain.Products;

namespace Pos.Application.Products;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(ProductId productId, CancellationToken cancellationToken);

    Task<Product?> GetBySkuAsync(OrganizationId organizationId, Sku sku, CancellationToken cancellationToken);

    Task<IReadOnlyList<Product>> GetByOrganizationAsync(OrganizationId organizationId, CancellationToken cancellationToken);

    Task AddAsync(Product product, CancellationToken cancellationToken);
}
