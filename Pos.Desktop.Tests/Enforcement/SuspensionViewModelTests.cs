using Pos.Application.Enforcement;
using Pos.Desktop.Enforcement;

namespace Pos.Desktop.Tests.Enforcement;

public class SuspensionViewModelTests
{
    // Corrección de la tarea (sección 7-9): la pantalla de suspensión debe ofrecer una vía
    // acotada para cerrar una caja ya abierta sin resolver el diálogo restrictivo ni exponer el
    // resto del POS. App.xaml.cs es quien orquesta login + cierre; aquí solo se prueba que la
    // ViewModel pide la acción.
    [Fact]
    public void CloseOpenRegisterCommandRaisesCloseOpenRegisterRequested()
    {
        var enforcementStateService = new FakeInstallationEnforcementStateService(InstallationEnforcementState.Suspended);
        var viewModel = new SuspensionViewModel(enforcementStateService);

        var raised = false;
        viewModel.CloseOpenRegisterRequested += (_, _) => raised = true;

        viewModel.CloseOpenRegisterCommand.Execute(null);

        Assert.True(raised);
    }

    // Pedir cerrar la caja abierta no es una recuperación: el estado de enforcement no cambia ni
    // se dispara Resolved (eso solo lo hace un heartbeat exitoso).
    [Fact]
    public void CloseOpenRegisterCommandDoesNotResolveTheSuspensionScreen()
    {
        var enforcementStateService = new FakeInstallationEnforcementStateService(InstallationEnforcementState.Suspended);
        var viewModel = new SuspensionViewModel(enforcementStateService);

        var resolved = false;
        viewModel.Resolved += (_, _) => resolved = true;

        viewModel.CloseOpenRegisterCommand.Execute(null);

        Assert.False(resolved);
    }
}
