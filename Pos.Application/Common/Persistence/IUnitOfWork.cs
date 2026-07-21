namespace Pos.Application.Common.Persistence;

public interface IUnitOfWork
{
    Task CommitAsync(CancellationToken cancellationToken);
}
