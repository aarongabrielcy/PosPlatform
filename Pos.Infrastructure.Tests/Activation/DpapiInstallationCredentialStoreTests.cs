using System.Runtime.Versioning;
using Microsoft.Extensions.Logging.Abstractions;
using Pos.Infrastructure.Activation;
using Pos.Infrastructure.Storage;

namespace Pos.Infrastructure.Tests.Activation;

// DpapiInstallationCredentialStore usa DPAPI (CryptProtectData/CryptUnprotectData), exclusivo de
// Windows; esta clase de prueba se anota igual en vez de suprimir CA1416, siguiendo el mismo
// criterio que DependencyInjection.AddPosInfrastructure (ver comentario allí).
[SupportedOSPlatform("windows")]
public class DpapiInstallationCredentialStoreTests
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
    public async Task SaveThenTryLoadRoundTripsTheExactCredential()
    {
        var store = CreateStore(out var cleanup);
        try
        {
            const string credential = "cred-abc123.def456";

            var saved = await store.SaveAsync(credential, CancellationToken.None);

            Assert.True(saved);
            Assert.Equal(credential, await store.TryLoadAsync(CancellationToken.None));
        }
        finally
        {
            cleanup();
        }
    }

    // La credencial nunca debe llegar a disco en texto plano (ver sección 13 de la tarea): esto
    // prueba que DPAPI efectivamente transforma el contenido, no solo que el round-trip funciona.
    [Fact]
    public async Task SavedFileDoesNotContainThePlaintextCredential()
    {
        var (store, pathProvider) = CreateStoreWithPathProvider(out var cleanup);
        try
        {
            const string credential = "super-secret-installation-credential-value";

            await store.SaveAsync(credential, CancellationToken.None);

            var filePath = Path.Combine(pathProvider.DataDirectory, "Activation", "credential.protected");
            var rawBytes = await File.ReadAllBytesAsync(filePath);
            var rawText = System.Text.Encoding.UTF8.GetString(rawBytes);

            Assert.DoesNotContain(credential, rawText, StringComparison.Ordinal);
        }
        finally
        {
            cleanup();
        }
    }

    [Fact]
    public async Task SecondSaveOverwritesThePreviouslyStoredCredential()
    {
        var store = CreateStore(out var cleanup);
        try
        {
            await store.SaveAsync("first-credential", CancellationToken.None);
            await store.SaveAsync("second-credential", CancellationToken.None);

            Assert.Equal("second-credential", await store.TryLoadAsync(CancellationToken.None));
        }
        finally
        {
            cleanup();
        }
    }

    private static DpapiInstallationCredentialStore CreateStore(out Action cleanup)
    {
        var (store, _) = CreateStoreWithPathProvider(out cleanup);
        return store;
    }

    private static (DpapiInstallationCredentialStore Store, ApplicationPathProvider PathProvider) CreateStoreWithPathProvider(
        out Action cleanup)
    {
        var root = Path.Combine(Path.GetTempPath(), "PosPlatformCredentialStoreTests_" + Guid.NewGuid());
        var pathProvider = new ApplicationPathProvider(root);
        var store = new DpapiInstallationCredentialStore(pathProvider, NullLogger<DpapiInstallationCredentialStore>.Instance);

        cleanup = () =>
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        };

        return (store, pathProvider);
    }
}
