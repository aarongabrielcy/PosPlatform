using Pos.Application.Authentication;
using Pos.Domain.Security;

namespace Pos.Application.ProductAudit;

public sealed class ProductAuditService : IProductAuditService
{
    private readonly ICurrentUserSession _currentUserSession;
    private readonly IProductAuditQuery _productAuditQuery;

    public ProductAuditService(ICurrentUserSession currentUserSession, IProductAuditQuery productAuditQuery)
    {
        _currentUserSession = currentUserSession ?? throw new ArgumentNullException(nameof(currentUserSession));
        _productAuditQuery = productAuditQuery ?? throw new ArgumentNullException(nameof(productAuditQuery));
    }

    public async Task<ProductAuditPageResult> SearchPageAsync(
        ProductAuditFilter filter, int skip, int take, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var user = _currentUserSession.CurrentUser;

        if (user is null || !user.HasPermission(Permission.ViewProductAudit))
        {
            return ProductAuditPageResult.Empty;
        }

        return await _productAuditQuery.SearchPageAsync(user.OrganizationId, filter, skip, take, cancellationToken);
    }
}
