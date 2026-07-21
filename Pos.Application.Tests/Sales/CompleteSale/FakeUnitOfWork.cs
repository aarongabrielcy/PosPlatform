using Pos.Application.Common.Persistence;

namespace Pos.Application.Tests.Sales.CompleteSale;

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    private readonly List<string>? _operationLog;

    public FakeUnitOfWork(List<string>? operationLog = null)
    {
        _operationLog = operationLog;
    }

    public int CommitCallCount { get; private set; }

    public Task CommitAsync(CancellationToken cancellationToken)
    {
        CommitCallCount++;
        _operationLog?.Add("UnitOfWork.Commit");

        return Task.CompletedTask;
    }
}
