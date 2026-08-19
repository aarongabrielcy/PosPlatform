using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Enforcement;
using Pos.Infrastructure.Enforcement;
using Pos.Infrastructure.Storage;

namespace Pos.Infrastructure.Tests.Enforcement;

public class FileInstallationEnforcementStateStoreTests
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

    [Theory]
    [InlineData(InstallationEnforcementState.Suspended)]
    [InlineData(InstallationEnforcementState.CredentialInvalid)]
    [InlineData(InstallationEnforcementState.Decommissioned)]
    public async Task SaveThenTryLoadRoundTripsTheExactState(InstallationEnforcementState state)
    {
        var store = CreateStore(out var cleanup);
        try
        {
            var saved = await store.SaveAsync(state, CancellationToken.None);
            var loaded = await store.TryLoadAsync(CancellationToken.None);

            Assert.True(saved);
            Assert.Equal(state, loaded);
        }
        finally
        {
            cleanup();
        }
    }

    [Fact]
    public async Task SecondSaveAtomicallyReplacesThePreviouslyStoredState()
    {
        var store = CreateStore(out var cleanup);
        try
        {
            await store.SaveAsync(InstallationEnforcementState.Suspended, CancellationToken.None);
            await store.SaveAsync(InstallationEnforcementState.Decommissioned, CancellationToken.None);

            Assert.Equal(InstallationEnforcementState.Decommissioned, await store.TryLoadAsync(CancellationToken.None));
        }
        finally
        {
            cleanup();
        }
    }

    [Fact]
    public async Task SaveNeverLeavesATemporaryFileBehind()
    {
        var root = Path.Combine(Path.GetTempPath(), "PosPlatformEnforcementStoreTests_" + Guid.NewGuid());
        var pathProvider = new ApplicationPathProvider(root);
        var store = new FileInstallationEnforcementStateStore(pathProvider, NullLogger<FileInstallationEnforcementStateStore>.Instance);

        try
        {
            await store.SaveAsync(InstallationEnforcementState.Suspended, CancellationToken.None);
            await store.SaveAsync(InstallationEnforcementState.CredentialInvalid, CancellationToken.None);

            var enforcementDirectory = Path.Combine(pathProvider.DataDirectory, "Enforcement");
            var files = Directory.GetFiles(enforcementDirectory);

            Assert.Single(files);
            Assert.DoesNotContain(files, f => f.EndsWith(".tmp", StringComparison.Ordinal));
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
    public async Task ClearDeletesTheStateFileSoAllowedIsRepresentedByAbsence()
    {
        var store = CreateStore(out var cleanup);
        try
        {
            await store.SaveAsync(InstallationEnforcementState.Suspended, CancellationToken.None);

            var cleared = await store.ClearAsync(CancellationToken.None);

            Assert.True(cleared);
            Assert.Null(await store.TryLoadAsync(CancellationToken.None));
        }
        finally
        {
            cleanup();
        }
    }

    [Fact]
    public async Task ClearWhenNothingWasSavedStillSucceeds()
    {
        var store = CreateStore(out var cleanup);
        try
        {
            Assert.True(await store.ClearAsync(CancellationToken.None));
        }
        finally
        {
            cleanup();
        }
    }

    [Fact]
    public void SavingAllowedThrowsBecauseAllowedIsRepresentedByAbsenceNotByAnExplicitValue()
    {
        var store = CreateStore(out var cleanup);
        try
        {
            Assert.ThrowsAsync<ArgumentException>(
                () => store.SaveAsync(InstallationEnforcementState.Allowed, CancellationToken.None));
        }
        finally
        {
            cleanup();
        }
    }

    // Fail-safe de la sección 14 de la tarea: un archivo presente pero corrupto/ilegible nunca se
    // interpreta como Allowed. Se asume Suspended como valor de respaldo determinístico.
    [Fact]
    public async Task CorruptStateFileIsTreatedAsSuspendedNeverAsAllowed()
    {
        var root = Path.Combine(Path.GetTempPath(), "PosPlatformEnforcementStoreTests_" + Guid.NewGuid());
        var pathProvider = new ApplicationPathProvider(root);
        var store = new FileInstallationEnforcementStateStore(pathProvider, NullLogger<FileInstallationEnforcementStateStore>.Instance);

        try
        {
            var enforcementDirectory = Path.Combine(pathProvider.DataDirectory, "Enforcement");
            Directory.CreateDirectory(enforcementDirectory);
            await File.WriteAllTextAsync(Path.Combine(enforcementDirectory, "enforcement-state.json"), "{ not valid json");

            var loaded = await store.TryLoadAsync(CancellationToken.None);

            Assert.Equal(InstallationEnforcementState.Suspended, loaded);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static FileInstallationEnforcementStateStore CreateStore(out Action cleanup)
    {
        var root = Path.Combine(Path.GetTempPath(), "PosPlatformEnforcementStoreTests_" + Guid.NewGuid());
        var pathProvider = new ApplicationPathProvider(root);
        var store = new FileInstallationEnforcementStateStore(
            pathProvider,
            NullLogger<FileInstallationEnforcementStateStore>.Instance);

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
