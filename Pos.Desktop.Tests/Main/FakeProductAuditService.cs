using Pos.Application.ProductAudit;

namespace Pos.Desktop.Tests.Main;

internal sealed class FakeProductAuditService : IProductAuditService
{
    public ProductAuditFilter? LastFilter { get; private set; }

    public Task<ProductAuditPageResult> SearchPageAsync(
        ProductAuditFilter filter, int skip, int take, CancellationToken cancellationToken = default)
    {
        LastFilter = filter;

        return Task.FromResult(ProductAuditPageResult.Empty);
    }
}
