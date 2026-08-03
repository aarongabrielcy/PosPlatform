using Pos.Application.ProductAudit;

namespace Pos.Desktop.Tests.Audit.Products;

internal sealed class FakeProductAuditService : IProductAuditService
{
    private readonly Func<ProductAuditFilter, int, int, CancellationToken, Task<ProductAuditPageResult>>? _handler;

    public FakeProductAuditService(
        Func<ProductAuditFilter, int, int, CancellationToken, Task<ProductAuditPageResult>>? handler = null)
    {
        _handler = handler;
    }

    public int SearchPageCallCount { get; private set; }

    public ProductAuditFilter? LastFilter { get; private set; }

    public int? LastSkip { get; private set; }

    public int? LastTake { get; private set; }

    public Task<ProductAuditPageResult> SearchPageAsync(
        ProductAuditFilter filter, int skip, int take, CancellationToken cancellationToken = default)
    {
        SearchPageCallCount++;
        LastFilter = filter;
        LastSkip = skip;
        LastTake = take;

        return _handler is null
            ? Task.FromResult(ProductAuditPageResult.Empty)
            : _handler(filter, skip, take, cancellationToken);
    }
}
