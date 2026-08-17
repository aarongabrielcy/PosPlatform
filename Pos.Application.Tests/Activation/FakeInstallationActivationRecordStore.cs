using Pos.Application.Activation;

namespace Pos.Application.Tests.Activation;

internal sealed class FakeInstallationActivationRecordStore : IInstallationActivationRecordStore
{
    private readonly bool _saveSucceeds;
    private InstallationActivationRecord? _storedRecord;

    public FakeInstallationActivationRecordStore(bool saveSucceeds = true, InstallationActivationRecord? initialRecord = null)
    {
        _saveSucceeds = saveSucceeds;
        _storedRecord = initialRecord;
    }

    public int SaveCallCount { get; private set; }

    public InstallationActivationRecord? LastSavedRecord { get; private set; }

    public Task<bool> SaveAsync(InstallationActivationRecord record, CancellationToken cancellationToken)
    {
        SaveCallCount++;
        LastSavedRecord = record;

        if (_saveSucceeds)
        {
            _storedRecord = record;
        }

        return Task.FromResult(_saveSucceeds);
    }

    public Task<InstallationActivationRecord?> TryLoadAsync(CancellationToken cancellationToken) =>
        Task.FromResult(_storedRecord);
}
