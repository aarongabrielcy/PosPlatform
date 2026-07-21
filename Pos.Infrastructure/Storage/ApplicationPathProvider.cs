namespace Pos.Infrastructure.Storage;

public sealed class ApplicationPathProvider : IApplicationPathProvider
{
    private const string ApplicationFolderName = "PosPlatform";
    private const string DataFolderName = "Data";
    private const string DatabaseFileName = "pos.db";

    public ApplicationPathProvider()
        : this(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))
    {
    }

    public ApplicationPathProvider(string localApplicationDataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localApplicationDataDirectory);

        DataDirectory = Path.Combine(localApplicationDataDirectory, ApplicationFolderName, DataFolderName);
        DatabasePath = Path.Combine(DataDirectory, DatabaseFileName);
    }

    public string DataDirectory { get; }

    public string DatabasePath { get; }

    public void EnsureDataDirectoryExists()
    {
        Directory.CreateDirectory(DataDirectory);
    }
}
