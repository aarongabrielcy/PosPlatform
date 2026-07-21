namespace Pos.Infrastructure.Persistence.Initialization;

public interface ILocalDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
