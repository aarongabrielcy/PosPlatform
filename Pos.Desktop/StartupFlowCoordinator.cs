using Pos.Application.Installation;

namespace Pos.Desktop;

// Decisión pura del flujo de arranque, sin dependencias de WPF ni de IServiceProvider, para
// poder cubrir con pruebas automatizadas la lógica que en App.xaml.cs decide si se muestra
// MainWindow, se abre el diálogo de configuración inicial o se cierra la aplicación.
internal enum StartupFlowDecision
{
    ShowSetupDialog,
    ShowMainWindow,
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
            _ => StartupFlowDecision.ShowMainWindow,
        };

    public static StartupFlowDecision DecideForSetupDialogResult(bool? dialogResult) =>
        dialogResult == true ? StartupFlowDecision.ShowMainWindow : StartupFlowDecision.ShutdownCancelled;
}
