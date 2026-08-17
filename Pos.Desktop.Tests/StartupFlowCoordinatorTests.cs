using Pos.Application.Activation;
using Pos.Application.Installation;
using Pos.Application.RegisterSessions;
using Pos.Desktop.RegisterSessions;

namespace Pos.Desktop.Tests;

public class StartupFlowCoordinatorTests
{
    [Fact]
    public void NotActivatedDecidesToShowActivationDialog() =>
        Assert.Equal(
            StartupFlowDecision.ShowActivationDialog,
            StartupFlowCoordinator.DecideForActivationStatus(ActivationStatus.NotActivated));

    [Fact]
    public void ActivatedDecidesToContinueAfterActivationNotDirectlyToSetupOrLogin() =>
        Assert.Equal(
            StartupFlowDecision.ContinueAfterActivation,
            StartupFlowCoordinator.DecideForActivationStatus(ActivationStatus.Activated));

    [Fact]
    public void SuccessfulActivationDialogDecidesToContinueAfterActivation() =>
        Assert.Equal(
            StartupFlowDecision.ContinueAfterActivation,
            StartupFlowCoordinator.DecideForActivationDialogResult(true));

    [Fact]
    public void CancelledActivationDialogDecidesToShutdown() =>
        Assert.Equal(
            StartupFlowDecision.ShutdownCancelled,
            StartupFlowCoordinator.DecideForActivationDialogResult(false));

    [Fact]
    public void ClosedActivationDialogWithoutResultDecidesToShutdown() =>
        Assert.Equal(
            StartupFlowDecision.ShutdownCancelled,
            StartupFlowCoordinator.DecideForActivationDialogResult(null));

    [Fact]
    public void RequiresSetupDecidesToShowSetupDialog() =>
        Assert.Equal(
            StartupFlowDecision.ShowSetupDialog,
            StartupFlowCoordinator.DecideForInstallationState(InstallationState.RequiresSetup));

    [Fact]
    public void InitializedDecidesToShowLoginNotMainWindow() =>
        Assert.Equal(
            StartupFlowDecision.ShowLogin,
            StartupFlowCoordinator.DecideForInstallationState(InstallationState.Initialized));

    [Fact]
    public void InvalidStateDecidesToShutdownWithoutShowingAnyWindow() =>
        Assert.Equal(
            StartupFlowDecision.ShutdownInvalidState,
            StartupFlowCoordinator.DecideForInstallationState(InstallationState.InvalidState));

    [Fact]
    public void SuccessfulSetupDialogDecidesToShowLoginNotMainWindow() =>
        Assert.Equal(
            StartupFlowDecision.ShowLogin,
            StartupFlowCoordinator.DecideForSetupDialogResult(true));

    [Fact]
    public void CancelledSetupDialogDecidesToShutdown() =>
        Assert.Equal(
            StartupFlowDecision.ShutdownCancelled,
            StartupFlowCoordinator.DecideForSetupDialogResult(false));

    [Fact]
    public void ClosedSetupDialogWithoutResultDecidesToShutdown() =>
        Assert.Equal(
            StartupFlowDecision.ShutdownCancelled,
            StartupFlowCoordinator.DecideForSetupDialogResult(null));

    [Fact]
    public void SuccessfulLoginDialogDecidesToContinueAfterLoginNotMainWindow() =>
        Assert.Equal(
            StartupFlowDecision.ContinueAfterLogin,
            StartupFlowCoordinator.DecideForLoginDialogResult(true));

    [Fact]
    public void CancelledLoginDialogDecidesToShutdown() =>
        Assert.Equal(
            StartupFlowDecision.ShutdownCancelled,
            StartupFlowCoordinator.DecideForLoginDialogResult(false));

    [Fact]
    public void ClosedLoginDialogWithoutResultDecidesToShutdown() =>
        Assert.Equal(
            StartupFlowDecision.ShutdownCancelled,
            StartupFlowCoordinator.DecideForLoginDialogResult(null));

    [Fact]
    public void OpenRegisterSessionStatusDecidesToShowMainWindow() =>
        Assert.Equal(
            StartupFlowDecision.ShowMainWindowAfterLogin,
            StartupFlowCoordinator.DecideForRegisterSessionStatus(RegisterSessionStatus.Open));

    [Fact]
    public void NoneOpenRegisterSessionStatusDecidesToShowOpenRegisterSessionDialog() =>
        Assert.Equal(
            StartupFlowDecision.ShowOpenRegisterSessionDialog,
            StartupFlowCoordinator.DecideForRegisterSessionStatus(RegisterSessionStatus.NoneOpen));

    [Fact]
    public void InvalidRegisterSessionStatusDecidesToShutdown() =>
        Assert.Equal(
            StartupFlowDecision.ShutdownInvalidRegisterSessionState,
            StartupFlowCoordinator.DecideForRegisterSessionStatus(RegisterSessionStatus.InvalidState));

    [Fact]
    public void RegisterOpenedResultDecidesToShowMainWindow() =>
        Assert.Equal(
            StartupFlowDecision.ShowMainWindowAfterLogin,
            StartupFlowCoordinator.DecideForOpenRegisterSessionResult(OpenRegisterSessionWindowResult.RegisterOpened));

    [Fact]
    public void LogoutRequestedResultDecidesToShowLoginNotShutdown() =>
        Assert.Equal(
            StartupFlowDecision.ShowLogin,
            StartupFlowCoordinator.DecideForOpenRegisterSessionResult(OpenRegisterSessionWindowResult.LogoutRequested));

    [Fact]
    public void ExitRequestedResultDecidesToShutdown() =>
        Assert.Equal(
            StartupFlowDecision.ShutdownCancelled,
            StartupFlowCoordinator.DecideForOpenRegisterSessionResult(OpenRegisterSessionWindowResult.ExitRequested));
}
