using Pos.Application.Activation;

namespace Pos.Application.Tests.Activation;

internal sealed class FakeInstallationCredentialStore : IInstallationCredentialStore
{
    private readonly bool _saveSucceeds;
    private string? _storedCredential;

    public FakeInstallationCredentialStore(bool saveSucceeds = true, string? initialCredential = null)
    {
        _saveSucceeds = saveSucceeds;
        _storedCredential = initialCredential;
    }

    public int SaveCallCount { get; private set; }

    public string? LastSavedCredential { get; private set; }

    public Task<bool> SaveAsync(string credential, CancellationToken cancellationToken)
    {
        SaveCallCount++;
        LastSavedCredential = credential;

        if (_saveSucceeds)
        {
            _storedCredential = credential;
        }

        return Task.FromResult(_saveSucceeds);
    }

    public Task<string?> TryLoadAsync(CancellationToken cancellationToken) => Task.FromResult(_storedCredential);
}
