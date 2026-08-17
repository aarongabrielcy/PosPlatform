using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Activation;
using Pos.Desktop.Activation;

namespace Pos.Desktop.Tests.Activation;

public class ActivationViewModelTests
{
    [Fact]
    public void BlankEnrollmentCodeShowsValidationErrorWithoutCallingTheService()
    {
        var service = SucceedingService();
        var viewModel = CreateViewModel(service);
        viewModel.EnrollmentCode = "   ";

        viewModel.ActivateCommand.Execute(null);

        Assert.Equal("Ingrese el código de activación.", viewModel.GeneralError);
        Assert.Equal(0, service.EnrollCallCount);
    }

    [Fact]
    public void DoubleSubmitWhileRunningIsIgnored()
    {
        var workSource = new TaskCompletionSource<EnrollmentOutcome>();
        var service = new FakeInstallationActivationStateService((_, _) => workSource.Task);
        var viewModel = CreateViewModel(service);
        viewModel.EnrollmentCode = "ABCD-1234";

        viewModel.ActivateCommand.Execute(null);
        viewModel.ActivateCommand.Execute(null);

        Assert.Equal(1, service.EnrollCallCount);

        workSource.SetResult(new EnrollmentOutcome(EnrollmentOutcomeStatus.Activated));
    }

    [Fact]
    public async Task IsBusyIsTrueWhileActivatingAndFalseAfterCompletion()
    {
        var workSource = new TaskCompletionSource<EnrollmentOutcome>();
        var service = new FakeInstallationActivationStateService((_, _) => workSource.Task);
        var viewModel = CreateViewModel(service);
        viewModel.EnrollmentCode = "ABCD-1234";

        var completionSignal = new TaskCompletionSource();
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ActivationViewModel.IsBusy) && !viewModel.IsBusy)
            {
                completionSignal.TrySetResult();
            }
        };

        viewModel.ActivateCommand.Execute(null);

        Assert.True(viewModel.IsBusy);

        workSource.SetResult(new EnrollmentOutcome(EnrollmentOutcomeStatus.Activated));
        await completionSignal.Task;

        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public void ActivatedOutcomeRaisesActivationCompleted()
    {
        var service = SucceedingService();
        var viewModel = CreateViewModel(service);
        viewModel.EnrollmentCode = "ABCD-1234";

        var raised = false;
        viewModel.ActivationCompleted += (_, _) => raised = true;

        viewModel.ActivateCommand.Execute(null);

        Assert.True(raised);
        Assert.Null(viewModel.GeneralError);
    }

    [Fact]
    public void RejectedOutcomeShowsGenericInvalidCodeMessageWithoutRaisingCompletion()
    {
        var service = new FakeInstallationActivationStateService(
            new EnrollmentOutcome(EnrollmentOutcomeStatus.EnrollmentRejected));
        var viewModel = CreateViewModel(service);
        viewModel.EnrollmentCode = "ABCD-1234";

        var raised = false;
        viewModel.ActivationCompleted += (_, _) => raised = true;

        viewModel.ActivateCommand.Execute(null);

        Assert.False(raised);
        Assert.Contains("no es válido", viewModel.GeneralError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NetworkFailureOutcomeShowsConnectivityMessageWithoutRaisingCompletion()
    {
        var service = new FakeInstallationActivationStateService(
            new EnrollmentOutcome(EnrollmentOutcomeStatus.NetworkFailure));
        var viewModel = CreateViewModel(service);
        viewModel.EnrollmentCode = "ABCD-1234";

        var raised = false;
        viewModel.ActivationCompleted += (_, _) => raised = true;

        viewModel.ActivateCommand.Execute(null);

        Assert.False(raised);
        Assert.Contains("conectar", viewModel.GeneralError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LocalPersistenceFailedOutcomeAsksForANewRecoveryCodeWithoutRaisingCompletion()
    {
        var service = new FakeInstallationActivationStateService(
            new EnrollmentOutcome(EnrollmentOutcomeStatus.LocalPersistenceFailed));
        var viewModel = CreateViewModel(service);
        viewModel.EnrollmentCode = "ABCD-1234";

        var raised = false;
        viewModel.ActivationCompleted += (_, _) => raised = true;

        viewModel.ActivateCommand.Execute(null);

        Assert.False(raised);
        Assert.Contains("recuperación", viewModel.GeneralError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnexpectedExceptionShowsGenericErrorAndDoesNotRaiseActivationCompleted()
    {
        var service = new FakeInstallationActivationStateService(
            (_, _) => throw new InvalidOperationException("Detalle técnico interno."));
        var viewModel = CreateViewModel(service);
        viewModel.EnrollmentCode = "ABCD-1234";

        var raised = false;
        viewModel.ActivationCompleted += (_, _) => raised = true;

        viewModel.ActivateCommand.Execute(null);

        Assert.False(raised);
        Assert.False(viewModel.IsBusy);
        Assert.NotNull(viewModel.GeneralError);
        Assert.DoesNotContain("Detalle técnico interno.", viewModel.GeneralError, StringComparison.Ordinal);
    }

    // El recorte de espacios es responsabilidad de InstallationActivationStateService (ver
    // InstallationActivationStateServiceTests): el ViewModel solo valida que no esté en blanco y
    // reenvía el valor tal cual.
    [Fact]
    public void NonBlankEnrollmentCodeIsForwardedAsIsToTheService()
    {
        var service = SucceedingService();
        var viewModel = CreateViewModel(service);
        viewModel.EnrollmentCode = "  ABCD-1234  ";

        viewModel.ActivateCommand.Execute(null);

        Assert.Equal("  ABCD-1234  ", service.LastEnrollmentCode);
    }

    private static ActivationViewModel CreateViewModel(FakeInstallationActivationStateService service) =>
        new(service, NullLogger<ActivationViewModel>.Instance);

    private static FakeInstallationActivationStateService SucceedingService() =>
        new(new EnrollmentOutcome(EnrollmentOutcomeStatus.Activated));
}
