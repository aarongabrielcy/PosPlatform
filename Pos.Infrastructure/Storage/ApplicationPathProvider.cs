namespace Pos.Infrastructure.Storage;

public sealed class ApplicationPathProvider : IApplicationPathProvider
{
    private const string ApplicationFolderName = "PosPlatform";
    private const string DataFolderName = "Data";
    private const string DatabaseFileName = "pos.db";
    private const string BackupFolderName = "Backups";
    private const string ProductImagesFolderName = "ProductImages";

    public ApplicationPathProvider()
        : this(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))
    {
    }

    public ApplicationPathProvider(string localApplicationDataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localApplicationDataDirectory);

        DataDirectory = Path.Combine(localApplicationDataDirectory, ApplicationFolderName, DataFolderName);
        DatabasePath = Path.Combine(DataDirectory, DatabaseFileName);
        BackupDirectory = Path.Combine(DataDirectory, BackupFolderName);
        ProductImagesDirectory = Path.Combine(DataDirectory, ProductImagesFolderName);
    }

    public string DataDirectory { get; }

    public string DatabasePath { get; }

    public string BackupDirectory { get; }

    public string ProductImagesDirectory { get; }

    public void EnsureDataDirectoryExists()
    {
        Directory.CreateDirectory(DataDirectory);
    }

    public void EnsureBackupDirectoryExists()
    {
        Directory.CreateDirectory(BackupDirectory);
    }

    public void EnsureProductImagesDirectoryExists()
    {
        Directory.CreateDirectory(ProductImagesDirectory);
    }
}
