using Pos.Application.Installation;

namespace Pos.Desktop;

// Decisión pura del flujo de arranque, sin dependencias de WPF ni de IServiceProvider, para
// poder cubrir con pruebas automatizadas la lógica que en App.xaml.cs decide si se muestra
// LoginWindow, se abre el diálogo de configuración inicial o se cierra la aplicación. MainWindow
// solo se muestra después de un login exitoso (ver App.ShowMainWindow), nunca directamente desde
// este coordinador.
internal enum StartupFlowDecision
{
    ShowSetupDialog,
    ShowLogin,
    ShowMainWindowAfterLogin,
    ShutdownCancelled,
    ShutdownInvalidState,
}

internal static class StartupFlowCoordinator
{
    public static StartupFlowDecision DecideForInstallationState(InstallationState installationState) =>
        installationState switch
        {
            InstallationState.InvalidState => StartupFlowDecision.ShutdownInvalidState,
            InstallationState.RequiresSetup => StartupFlowDecision.ShowSetupDialog,
            _ => StartupFlowDecision.ShowLogin,
        };

    public static StartupFlowDecision DecideForSetupDialogResult(bool? dialogResult) =>
        dialogResult == true ? StartupFlowDecision.ShowLogin : StartupFlowDecision.ShutdownCancelled;

    public static StartupFlowDecision DecideForLoginDialogResult(bool? dialogResult) =>
        dialogResult == true ? StartupFlowDecision.ShowMainWindowAfterLogin : StartupFlowDecision.ShutdownCancelled;
}
