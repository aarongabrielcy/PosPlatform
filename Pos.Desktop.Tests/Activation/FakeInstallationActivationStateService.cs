using Pos.Application.Activation;

namespace Pos.Desktop.Tests.Activation;

internal sealed class FakeInstallationActivationStateService : IInstallationActivationStateService
{
    private readonly Func<string, CancellationToken, Task<EnrollmentOutcome>> _enrollAsync;

    public FakeInstallationActivationStateService(EnrollmentOutcome result)
        : this((_, _) => Task.FromResult(result))
    {
    }

    public FakeInstallationActivationStateService(Func<string, CancellationToken, Task<EnrollmentOutcome>> enrollAsync)
    {
        _enrollAsync = enrollAsync;
    }

    public int EnrollCallCount { get; private set; }

    public string? LastEnrollmentCode { get; private set; }

    public int RecoverCallCount { get; private set; }

    public string? LastRecoveryCode { get; private set; }

    public int ActivateAsNewInstallationCallCount { get; private set; }

    public string? LastNewInstallationCode { get; private set; }

    public Task<ActivationStatus> GetActivationStatusAsync(CancellationToken cancellationToken) =>
        Task.FromResult(ActivationStatus.NotActivated);

    public Task<EnrollmentOutcome> EnrollAsync(string enrollmentCode, CancellationToken cancellationToken)
    {
        EnrollCallCount++;
        LastEnrollmentCode = enrollmentCode;

        return _enrollAsync(enrollmentCode, cancellationToken);
    }

    public Task<EnrollmentOutcome> RecoverCredentialAsync(string recoveryEnrollmentCode, CancellationToken cancellationToken)
    {
        RecoverCallCount++;
        LastRecoveryCode = recoveryEnrollmentCode;

        return _enrollAsync(recoveryEnrollmentCode, cancellationToken);
    }

    public Task<EnrollmentOutcome> ActivateAsNewInstallationAsync(string enrollmentCode, CancellationToken cancellationToken)
    {
        ActivateAsNewInstallationCallCount++;
        LastNewInstallationCode = enrollmentCode;

        return _enrollAsync(enrollmentCode, cancellationToken);
    }
}
