using Pos.Application.ProductAudit;
using Pos.Domain.ProductAudit;

namespace Pos.Application.Tests.Products.ManageProduct;

internal sealed class FakeProductAuditRepository : IProductAuditRepository
{
    private readonly List<ProductAuditEvent> _addedEvents = [];

    public int AddCallCount { get; private set; }

    public IReadOnlyList<ProductAuditEvent> AddedEvents => _addedEvents;

    public Task AddAsync(ProductAuditEvent auditEvent, CancellationToken cancellationToken)
    {
        AddCallCount++;
        _addedEvents.Add(auditEvent);

        return Task.CompletedTask;
    }
}
