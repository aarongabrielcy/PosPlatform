using Pos.Application.Configuration;

namespace Pos.Desktop.Tests.LocalConfiguration;

internal sealed class FakeLocalSettingsStore : ILocalSettingsStore
{
    public LocalSettings? Loaded { get; set; }

    public LocalSettings? LastSaved { get; private set; }

    public bool SaveResult { get; set; } = true;

    public int SaveCallCount { get; private set; }

    public Task<LocalSettings?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Loaded);

    public Task<bool> SaveAsync(LocalSettings settings, CancellationToken cancellationToken = default)
    {
        SaveCallCount++;
        LastSaved = settings;

        return Task.FromResult(SaveResult);
    }
}
