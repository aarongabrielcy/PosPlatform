using Pos.Application.InstallationHealth;

namespace Pos.Application.Tests.InstallationHealth;

internal sealed class FakeInstallationHealthClient : IInstallationHealthClient
{
    private readonly InstallationHeartbeatClientResult _result;

    public FakeInstallationHealthClient(InstallationHeartbeatClientResult result)
    {
        _result = result;
    }

    public int CallCount { get; private set; }

    public string? LastCredential { get; private set; }

    public string? LastAppVersion { get; private set; }

    public DateTimeOffset? LastClientReportedAtUtc { get; private set; }

    public Task<InstallationHeartbeatClientResult> SendHeartbeatAsync(
        string credential,
        string appVersion,
        DateTimeOffset clientReportedAtUtc,
        CancellationToken cancellationToken)
    {
        CallCount++;
        LastCredential = credential;
        LastAppVersion = appVersion;
        LastClientReportedAtUtc = clientReportedAtUtc;

        return Task.FromResult(_result);
    }
}
