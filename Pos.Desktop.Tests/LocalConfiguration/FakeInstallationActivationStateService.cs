using Pos.Application.Activation;

namespace Pos.Desktop.Tests.LocalConfiguration;

internal sealed class FakeInstallationActivationStateService : IInstallationActivationStateService
{
    public ActivationStatus Status { get; set; } = ActivationStatus.Activated;

    public Task<ActivationStatus> GetActivationStatusAsync(CancellationToken cancellationToken) => Task.FromResult(Status);

    public Task<EnrollmentOutcome> EnrollAsync(string enrollmentCode, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<EnrollmentOutcome> RecoverCredentialAsync(string recoveryEnrollmentCode, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<EnrollmentOutcome> ActivateAsNewInstallationAsync(string enrollmentCode, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
