using System.IO;
using Pos.Infrastructure.Storage;

namespace Pos.Desktop.Tests.LocalConfiguration;

internal sealed class FakeApplicationPathProvider : IApplicationPathProvider
{
    public FakeApplicationPathProvider(string root)
    {
        DataDirectory = Path.Combine(root, "PosPlatform", "Data");
        DatabasePath = Path.Combine(DataDirectory, "pos.db");
        BackupDirectory = Path.Combine(DataDirectory, "Backups");
    }

    public string DataDirectory { get; }

    public string DatabasePath { get; }

    public string BackupDirectory { get; }

    public string ProductImagesDirectory => Path.Combine(DataDirectory, "ProductImages");

    public void EnsureDataDirectoryExists()
    {
    }

    public void EnsureBackupDirectoryExists()
    {
    }

    public void EnsureProductImagesDirectoryExists()
    {
    }
}
