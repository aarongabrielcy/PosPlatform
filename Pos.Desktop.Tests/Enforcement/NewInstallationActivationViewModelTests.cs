using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Activation;
using Pos.Desktop.Enforcement;
using Pos.Desktop.Tests.Activation;

namespace Pos.Desktop.Tests.Enforcement;

public class NewInstallationActivationViewModelTests
{
    // Corrección de la tarea (sección 7-9): la pantalla de activación como nueva instalación
    // (Decommissioned) debe ofrecer una vía acotada para cerrar una caja ya abierta sin resolver
    // el diálogo restrictivo ni exponer el resto del POS. App.xaml.cs es quien orquesta login +
    // cierre; aquí solo se prueba que la ViewModel pide la acción.
    [Fact]
    public void CloseOpenRegisterCommandRaisesCloseOpenRegisterRequested()
    {
        var service = new FakeInstallationActivationStateService(new EnrollmentOutcome(EnrollmentOutcomeStatus.Activated));
        var viewModel = new NewInstallationActivationViewModel(service, NullLogger<NewInstallationActivationViewModel>.Instance);

        var raised = false;
        viewModel.CloseOpenRegisterRequested += (_, _) => raised = true;

        viewModel.CloseOpenRegisterCommand.Execute(null);

        Assert.True(raised);
        Assert.Equal(0, service.ActivateAsNewInstallationCallCount);
    }

    // Mientras una activación está en curso (IsBusy), tampoco debe poder iniciarse el flujo de
    // cierre de caja: ambas acciones comparten la misma pantalla y no deben solaparse.
    [Fact]
    public void CloseOpenRegisterCommandIsDisabledWhileActivationIsInProgress()
    {
        var workSource = new TaskCompletionSource<EnrollmentOutcome>();
        var service = new FakeInstallationActivationStateService((_, _) => workSource.Task);
        var viewModel = new NewInstallationActivationViewModel(service, NullLogger<NewInstallationActivationViewModel>.Instance);
        viewModel.EnrollmentCode = "ABCD-1234";

        viewModel.ActivateCommand.Execute(null);

        Assert.False(viewModel.CloseOpenRegisterCommand.CanExecute(null));

        workSource.SetResult(new EnrollmentOutcome(EnrollmentOutcomeStatus.Activated));
    }
}
