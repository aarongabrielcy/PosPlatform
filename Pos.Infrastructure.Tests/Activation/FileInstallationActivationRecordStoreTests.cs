using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Activation;
using Pos.Infrastructure.Activation;
using Pos.Infrastructure.Storage;

namespace Pos.Infrastructure.Tests.Activation;

public class FileInstallationActivationRecordStoreTests
{
    [Fact]
    public async Task TryLoadReturnsNullWhenNothingWasSaved()
    {
        var store = CreateStore(out var cleanup);
        try
        {
            Assert.Null(await store.TryLoadAsync(CancellationToken.None));
        }
        finally
        {
            cleanup();
        }
    }

    [Fact]
    public async Task SaveThenTryLoadRoundTripsTheExactRecord()
    {
        var store = CreateStore(out var cleanup);
        try
        {
            var record = new InstallationActivationRecord("inst-1234", new DateTimeOffset(2026, 8, 16, 12, 0, 0, TimeSpan.Zero));

            var saved = await store.SaveAsync(record, CancellationToken.None);
            var loaded = await store.TryLoadAsync(CancellationToken.None);

            Assert.True(saved);
            Assert.Equal(record, loaded);
        }
        finally
        {
            cleanup();
        }
    }

    [Fact]
    public async Task SecondSaveOverwritesThePreviouslyStoredRecord()
    {
        var store = CreateStore(out var cleanup);
        try
        {
            var first = new InstallationActivationRecord("inst-1", DateTimeOffset.UtcNow);
            var second = new InstallationActivationRecord("inst-2", DateTimeOffset.UtcNow);

            await store.SaveAsync(first, CancellationToken.None);
            await store.SaveAsync(second, CancellationToken.None);

            Assert.Equal(second, await store.TryLoadAsync(CancellationToken.None));
        }
        finally
        {
            cleanup();
        }
    }

    private static FileInstallationActivationRecordStore CreateStore(out Action cleanup)
    {
        var root = Path.Combine(Path.GetTempPath(), "PosPlatformRecordStoreTests_" + Guid.NewGuid());
        var pathProvider = new ApplicationPathProvider(root);
        var store = new FileInstallationActivationRecordStore(
            pathProvider,
            NullLogger<FileInstallationActivationRecordStore>.Instance);

        cleanup = () =>
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        };

        return store;
    }
}
