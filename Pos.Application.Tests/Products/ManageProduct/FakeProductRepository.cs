using Pos.Application.Products;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Products;

namespace Pos.Application.Tests.Products.ManageProduct;

internal sealed class FakeProductRepository : IProductRepository
{
    private readonly Dictionary<ProductId, Product> _products = new();

    public int UpdateCallCount { get; private set; }

    public int SearchCallCount { get; private set; }

    public void Add(Product product) => _products[product.Id] = product;

    public Task<Product?> GetByIdAsync(ProductId productId, CancellationToken cancellationToken) =>
        Task.FromResult(_products.TryGetValue(productId, out var product) ? product : null);

    public Task<Product?> GetBySkuAsync(OrganizationId organizationId, Sku sku, CancellationToken cancellationToken) =>
        Task.FromResult(_products.Values.SingleOrDefault(
            p => p.OrganizationId == organizationId && p.Sku.Value == sku.Value));

    public Task<Product?> GetByBarcodeAsync(OrganizationId organizationId, Barcode barcode, CancellationToken cancellationToken) =>
        Task.FromResult(_products.Values.FirstOrDefault(
            p => p.OrganizationId == organizationId && p.Barcode is not null && p.Barcode.Value.Value == barcode.Value));

    public Task<IReadOnlyList<Product>> GetByOrganizationAsync(
        OrganizationId organizationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Product>>(
            _products.Values.Where(p => p.OrganizationId == organizationId).ToList());

    public Task<IReadOnlyList<Product>> SearchActiveAsync(
        OrganizationId organizationId, string searchTerm, int maxResults, CancellationToken cancellationToken) =>
        throw new NotSupportedException("No se usa en las pruebas de ProductManagementService.");

    public Task<IReadOnlyList<Product>> SearchAsync(
        OrganizationId organizationId, string searchTerm, bool includeInactive, int maxResults, CancellationToken cancellationToken)
    {
        SearchCallCount++;

        var upperTerm = searchTerm.Trim().ToUpperInvariant();

        var matches = _products.Values
            .Where(p => p.OrganizationId == organizationId && (includeInactive || p.IsActive))
            .Where(p =>
                p.Sku.Value.Contains(upperTerm, StringComparison.OrdinalIgnoreCase) ||
                (p.Barcode is not null && p.Barcode.Value.Value.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)) ||
                p.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Name)
            .Take(maxResults)
            .ToList();

        return Task.FromResult<IReadOnlyList<Product>>(matches);
    }

    public Task AddAsync(Product product, CancellationToken cancellationToken)
    {
        _products[product.Id] = product;

        return Task.CompletedTask;
    }

    public Task UpdateAsync(Product product, CancellationToken cancellationToken)
    {
        UpdateCallCount++;
        _products[product.Id] = product;

        return Task.CompletedTask;
    }
}
