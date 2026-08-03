using Pos.Application.ProductAudit;
using Pos.Domain.Common.Identifiers;

namespace Pos.Application.Tests.Products.ManageProduct;

internal sealed class FakeProductAuditQuery : IProductAuditQuery
{
    private readonly IReadOnlyDictionary<ProductId, ProductRecentActivity> _recentActivity;

    public FakeProductAuditQuery(IReadOnlyDictionary<ProductId, ProductRecentActivity>? recentActivity = null)
    {
        _recentActivity = recentActivity ?? new Dictionary<ProductId, ProductRecentActivity>();
    }

    public int SearchPageCallCount { get; private set; }

    public int GetRecentActivityCallCount { get; private set; }

    public IReadOnlyList<ProductId>? LastProductIds { get; private set; }

    public DateTimeOffset? LastSinceUtc { get; private set; }

    public Task<ProductAuditPageResult> SearchPageAsync(
        OrganizationId organizationId, ProductAuditFilter filter, int skip, int take, CancellationToken cancellationToken)
    {
        SearchPageCallCount++;

        return Task.FromResult(ProductAuditPageResult.Empty);
    }

    public Task<IReadOnlyDictionary<ProductId, ProductRecentActivity>> GetRecentActivityAsync(
        OrganizationId organizationId,
        IReadOnlyList<ProductId> productIds,
        DateTimeOffset sinceUtc,
        CancellationToken cancellationToken)
    {
        GetRecentActivityCallCount++;
        LastProductIds = productIds;
        LastSinceUtc = sinceUtc;

        return Task.FromResult(_recentActivity);
    }
}
