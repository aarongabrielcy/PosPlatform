using Pos.Application.Enforcement;

namespace Pos.Application.Tests.Enforcement;

internal sealed class FakeInstallationEnforcementStateStore : IInstallationEnforcementStateStore
{
    private readonly bool _saveSucceeds;
    private readonly bool _clearSucceeds;
    private InstallationEnforcementState? _persisted;

    public FakeInstallationEnforcementStateStore(
        InstallationEnforcementState? initialPersisted = null, bool saveSucceeds = true, bool clearSucceeds = true)
    {
        _persisted = initialPersisted;
        _saveSucceeds = saveSucceeds;
        _clearSucceeds = clearSucceeds;
    }

    public int SaveCallCount { get; private set; }

    public InstallationEnforcementState? LastSavedState { get; private set; }

    public int ClearCallCount { get; private set; }

    public InstallationEnforcementState? CurrentlyPersisted => _persisted;

    public Task<InstallationEnforcementState?> TryLoadAsync(CancellationToken cancellationToken) =>
        Task.FromResult(_persisted);

    public Task<bool> SaveAsync(InstallationEnforcementState state, CancellationToken cancellationToken)
    {
        SaveCallCount++;
        LastSavedState = state;

        if (_saveSucceeds)
        {
            _persisted = state;
        }

        return Task.FromResult(_saveSucceeds);
    }

    public Task<bool> ClearAsync(CancellationToken cancellationToken)
    {
        ClearCallCount++;

        if (_clearSucceeds)
        {
            _persisted = null;
        }

        return Task.FromResult(_clearSucceeds);
    }
}
