namespace Pos.Infrastructure.Storage;

public interface IApplicationPathProvider
{
    string DataDirectory { get; }

    string DatabasePath { get; }

    void EnsureDataDirectoryExists();
}
