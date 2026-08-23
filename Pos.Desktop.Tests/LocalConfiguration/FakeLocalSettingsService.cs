using Pos.Application.Configuration;

namespace Pos.Desktop.Tests.LocalConfiguration;

internal sealed class FakeLocalSettingsService : ILocalSettingsService
{
    public LocalSettingsSaveResult Result { get; set; } = LocalSettingsSaveResult.Saved;

    public LocalSettings? LastSaved { get; private set; }

    public int SaveCallCount { get; private set; }

    public Task<LocalSettingsSaveResult> SaveAsync(LocalSettings settings, CancellationToken cancellationToken = default)
    {
        SaveCallCount++;
        LastSaved = settings;

        return Task.FromResult(Result);
    }
}
