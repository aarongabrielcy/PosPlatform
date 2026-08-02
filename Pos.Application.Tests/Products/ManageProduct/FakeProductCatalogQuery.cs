using Pos.Application.Products;
using Pos.Application.Products.ManageProduct;
using Pos.Domain.Common.Identifiers;

namespace Pos.Application.Tests.Products.ManageProduct;

internal sealed class FakeProductCatalogQuery : IProductCatalogQuery
{
    private readonly ProductCatalogPageResult _result;
    private readonly ProductCatalogSummary _summary;

    public FakeProductCatalogQuery(ProductCatalogPageResult? result = null, ProductCatalogSummary? summary = null)
    {
        _result = result ?? ProductCatalogPageResult.Empty;
        _summary = summary ?? ProductCatalogSummary.Empty;
    }

    public int GetSummaryCallCount { get; private set; }

    public int SearchPageCallCount { get; private set; }

    public OrganizationId? LastOrganizationId { get; private set; }

    public BranchId? LastBranchId { get; private set; }

    public string? LastSearchTerm { get; private set; }

    public ProductCatalogStatusFilter? LastFilter { get; private set; }

    public int? LastSkip { get; private set; }

    public int? LastTake { get; private set; }

    public Task<ProductCatalogPageResult> SearchPageAsync(
        OrganizationId organizationId,
        BranchId branchId,
        string? searchTerm,
        ProductCatalogStatusFilter filter,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        SearchPageCallCount++;
        LastOrganizationId = organizationId;
        LastBranchId = branchId;
        LastSearchTerm = searchTerm;
        LastFilter = filter;
        LastSkip = skip;
        LastTake = take;

        return Task.FromResult(_result);
    }

    public Task<ProductCatalogSummary> GetSummaryAsync(
        OrganizationId organizationId, BranchId branchId, CancellationToken cancellationToken)
    {
        GetSummaryCallCount++;
        LastOrganizationId = organizationId;
        LastBranchId = branchId;

        return Task.FromResult(_summary);
    }
}
