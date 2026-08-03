using Pos.Application.ProductAudit;

namespace Pos.Desktop.Tests.Main;

internal sealed class FakeProductAuditService : IProductAuditService
{
    public Task<ProductAuditPageResult> SearchPageAsync(
        ProductAuditFilter filter, int skip, int take, CancellationToken cancellationToken = default) =>
        Task.FromResult(ProductAuditPageResult.Empty);
}
