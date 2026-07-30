using Pos.Application.Installation;

namespace Pos.Desktop.Tests;

public class StartupFlowCoordinatorTests
{
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
    public void SuccessfulLoginDialogDecidesToShowMainWindow() =>
        Assert.Equal(
            StartupFlowDecision.ShowMainWindowAfterLogin,
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
}
