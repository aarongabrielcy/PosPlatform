using Pos.Application.Common.Persistence;

namespace Pos.Application.Tests.Products.ManageProduct;

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int CommitCallCount { get; private set; }

    public Task CommitAsync(CancellationToken cancellationToken)
    {
        CommitCallCount++;

        return Task.CompletedTask;
    }
}
