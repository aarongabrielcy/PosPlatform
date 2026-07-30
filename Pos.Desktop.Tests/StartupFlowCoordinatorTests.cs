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
    public void InitializedDecidesToShowMainWindowDirectly() =>
        Assert.Equal(
            StartupFlowDecision.ShowMainWindow,
            StartupFlowCoordinator.DecideForInstallationState(InstallationState.Initialized));

    [Fact]
    public void InvalidStateDecidesToShutdownWithoutShowingAnyWindow() =>
        Assert.Equal(
            StartupFlowDecision.ShutdownInvalidState,
            StartupFlowCoordinator.DecideForInstallationState(InstallationState.InvalidState));

    [Fact]
    public void SuccessfulSetupDialogDecidesToShowMainWindow() =>
        Assert.Equal(
            StartupFlowDecision.ShowMainWindow,
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
}
