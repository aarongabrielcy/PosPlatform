using Pos.Application.Activation;

namespace Pos.Application.Tests.Activation;

internal sealed class FakeInstallationActivationClient : IInstallationActivationClient
{
    private readonly InstallationEnrollmentClientResult _result;

    public FakeInstallationActivationClient(InstallationEnrollmentClientResult result)
    {
        _result = result;
    }

    public int CallCount { get; private set; }

    public string? LastEnrollmentCode { get; private set; }

    public Task<InstallationEnrollmentClientResult> EnrollAsync(string enrollmentCode, CancellationToken cancellationToken)
    {
        CallCount++;
        LastEnrollmentCode = enrollmentCode;

        return Task.FromResult(_result);
    }
}
