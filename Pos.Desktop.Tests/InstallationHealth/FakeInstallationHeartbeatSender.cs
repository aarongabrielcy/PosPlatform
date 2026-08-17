using Pos.Application.InstallationHealth;

namespace Pos.Desktop.Tests.InstallationHealth;

internal sealed class FakeInstallationHeartbeatSender : IInstallationHeartbeatSender
{
    private readonly Func<int, InstallationHeartbeatSendOutcome> _outcomeForCall;

    public FakeInstallationHeartbeatSender(InstallationHeartbeatSendOutcome outcome)
        : this(_ => outcome)
    {
    }

    public FakeInstallationHeartbeatSender(Func<int, InstallationHeartbeatSendOutcome> outcomeForCall)
    {
        _outcomeForCall = outcomeForCall;
    }

    private int _callCount;

    public int CallCount => _callCount;

    public Task<InstallationHeartbeatSendOutcome> SendHeartbeatAsync(CancellationToken cancellationToken)
    {
        var callNumber = Interlocked.Increment(ref _callCount);
        return Task.FromResult(_outcomeForCall(callNumber));
    }
}
