using Pos.Application.Common.Persistence;

namespace Pos.Application.Tests.Bootstrap;

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    private readonly Func<CancellationToken, Task>? _onCommit;

    public FakeUnitOfWork(Func<CancellationToken, Task>? onCommit = null)
    {
        _onCommit = onCommit;
    }

    public int CommitCallCount { get; private set; }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        CommitCallCount++;

        if (_onCommit is not null)
        {
            await _onCommit(cancellationToken);
        }
    }
}
