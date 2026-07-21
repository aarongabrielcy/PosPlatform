namespace Pos.Infrastructure.Storage;

public interface IApplicationPathProvider
{
    string DataDirectory { get; }

    string DatabasePath { get; }

    string BackupDirectory { get; }

    void EnsureDataDirectoryExists();

    void EnsureBackupDirectoryExists();
}
