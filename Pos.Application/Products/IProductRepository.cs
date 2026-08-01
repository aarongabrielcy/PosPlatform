using Pos.Domain.Common.Identifiers;
using Pos.Domain.Products;

namespace Pos.Application.Products;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(ProductId productId, CancellationToken cancellationToken);

    Task<Product?> GetBySkuAsync(OrganizationId organizationId, Sku sku, CancellationToken cancellationToken);

    Task<Product?> GetByBarcodeAsync(OrganizationId organizationId, Barcode barcode, CancellationToken cancellationToken);

    Task<IReadOnlyList<Product>> GetByOrganizationAsync(OrganizationId organizationId, CancellationToken cancellationToken);

    // Búsqueda para el carrito de venta: solo productos activos, coincidencia exacta de Sku/Barcode
    // primero, luego coincidencias parciales de Sku/Barcode/Name, limitada a maxResults.
    Task<IReadOnlyList<Product>> SearchActiveAsync(
        OrganizationId organizationId, string searchTerm, int maxResults, CancellationToken cancellationToken);

    Task AddAsync(Product product, CancellationToken cancellationToken);
}
