using Pos.Application.Installation;
using Pos.Application.RegisterSessions;
using Pos.Desktop.RegisterSessions;

namespace Pos.Desktop;

// Decisión pura del flujo de arranque, sin dependencias de WPF ni de IServiceProvider, para
// poder cubrir con pruebas automatizadas la lógica que en App.xaml.cs decide si se muestra
// LoginWindow, se abre el diálogo de configuración inicial o se cierra la aplicación. MainWindow
// solo se muestra después de un login exitoso y con una caja abierta (ver App.ShowMainWindow),
// nunca directamente desde este coordinador.
internal enum StartupFlowDecision
{
    ShowSetupDialog,
    ShowLogin,
    ContinueAfterLogin,
    ShowMainWindowAfterLogin,
    ShowOpenRegisterSessionDialog,
    ShutdownCancelled,
    ShutdownInvalidState,
    ShutdownInvalidRegisterSessionState,
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

    // Un login exitoso ya no muestra MainWindow directamente: primero debe determinarse si el
    // usuario tiene una caja abierta (ver DecideForRegisterSessionStatus).
    public static StartupFlowDecision DecideForLoginDialogResult(bool? dialogResult) =>
        dialogResult == true ? StartupFlowDecision.ContinueAfterLogin : StartupFlowDecision.ShutdownCancelled;

    public static StartupFlowDecision DecideForRegisterSessionStatus(RegisterSessionStatus status) =>
        status switch
        {
            RegisterSessionStatus.Open => StartupFlowDecision.ShowMainWindowAfterLogin,
            RegisterSessionStatus.NoneOpen => StartupFlowDecision.ShowOpenRegisterSessionDialog,
            _ => StartupFlowDecision.ShutdownInvalidRegisterSessionState,
        };

    // Reemplaza el antiguo bool? DialogResult, que no permitía distinguir un logout explícito de
    // un cierre por la X (ambos colapsaban al mismo desenlace). LogoutRequested vuelve a
    // LoginWindow sin terminar el proceso; ExitRequested sí lo termina, igual que antes.
    public static StartupFlowDecision DecideForOpenRegisterSessionResult(OpenRegisterSessionWindowResult result) =>
        result switch
        {
            OpenRegisterSessionWindowResult.RegisterOpened => StartupFlowDecision.ShowMainWindowAfterLogin,
            OpenRegisterSessionWindowResult.LogoutRequested => StartupFlowDecision.ShowLogin,
            _ => StartupFlowDecision.ShutdownCancelled,
        };
}
