using Pos.Infrastructure.Storage;

namespace Pos.Infrastructure.Tests.Storage;

public class ApplicationPathProviderTests
{
    [Fact]
    public void DataDirectoryIsRootedUnderLocalApplicationData()
    {
        var provider = new ApplicationPathProvider();
        var expectedRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        Assert.StartsWith(expectedRoot, provider.DataDirectory);
    }

    [Fact]
    public void DataDirectoryEndsWithPosPlatformData()
    {
        var provider = new ApplicationPathProvider();
        var expectedSuffix = Path.Combine("PosPlatform", "Data");

        Assert.EndsWith(expectedSuffix, provider.DataDirectory);
    }

    [Fact]
    public void DatabasePathEndsWithPosDb()
    {
        var provider = new ApplicationPathProvider();

        Assert.EndsWith("pos.db", provider.DatabasePath);
    }

    [Fact]
    public void DatabasePathIsInsideDataDirectory()
    {
        var provider = new ApplicationPathProvider();

        Assert.Equal(Path.Combine(provider.DataDirectory, "pos.db"), provider.DatabasePath);
    }

    [Fact]
    public void BackupDirectoryIsInsideDataDirectory()
    {
        var provider = new ApplicationPathProvider();

        Assert.Equal(Path.Combine(provider.DataDirectory, "Backups"), provider.BackupDirectory);
    }

    [Fact]
    public void DataDirectoryIsAPureFunctionOfTheInjectedRootNotOfProcessState()
    {
        var rootA = Path.Combine(Path.GetTempPath(), "PosPlatformPathProviderTests_" + Guid.NewGuid());
        var rootB = Path.Combine(Path.GetTempPath(), "PosPlatformPathProviderTests_" + Guid.NewGuid());

        var providerA = new ApplicationPathProvider(rootA);
        var providerB = new ApplicationPathProvider(rootB);

        Assert.Equal(Path.Combine(rootA, "PosPlatform", "Data"), providerA.DataDirectory);
        Assert.Equal(Path.Combine(rootB, "PosPlatform", "Data"), providerB.DataDirectory);
        Assert.NotEqual(providerA.DataDirectory, providerB.DataDirectory);
    }

    [Fact]
    public void ConstructorDoesNotCreateAnyFilesOrDirectories()
    {
        var root = Path.Combine(Path.GetTempPath(), "PosPlatformPathProviderTests_" + Guid.NewGuid());

        var provider = new ApplicationPathProvider(root);

        Assert.False(Directory.Exists(provider.DataDirectory));
        Assert.False(File.Exists(provider.DatabasePath));
        Assert.False(Directory.Exists(provider.BackupDirectory));
    }

    [Fact]
    public void EnsureDataDirectoryExistsCreatesTheDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "PosPlatformPathProviderTests_" + Guid.NewGuid());
        try
        {
            var provider = new ApplicationPathProvider(root);

            provider.EnsureDataDirectoryExists();

            Assert.True(Directory.Exists(provider.DataDirectory));
            Assert.False(Directory.Exists(provider.BackupDirectory));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void EnsureBackupDirectoryExistsCreatesTheDirectoryWithoutRequiringDataDirectoryFirst()
    {
        var root = Path.Combine(Path.GetTempPath(), "PosPlatformPathProviderTests_" + Guid.NewGuid());
        try
        {
            var provider = new ApplicationPathProvider(root);

            provider.EnsureBackupDirectoryExists();

            Assert.True(Directory.Exists(provider.BackupDirectory));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
