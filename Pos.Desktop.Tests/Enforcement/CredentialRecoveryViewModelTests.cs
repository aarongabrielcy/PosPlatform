using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Activation;
using Pos.Desktop.Enforcement;
using Pos.Desktop.Tests.Activation;

namespace Pos.Desktop.Tests.Enforcement;

public class CredentialRecoveryViewModelTests
{
    // Corrección de la tarea (sección 7-9): la pantalla de recuperación de credencial debe
    // ofrecer una vía acotada para cerrar una caja ya abierta sin resolver el diálogo restrictivo
    // ni exponer el resto del POS. App.xaml.cs es quien orquesta login + cierre; aquí solo se
    // prueba que la ViewModel pide la acción.
    [Fact]
    public void CloseOpenRegisterCommandRaisesCloseOpenRegisterRequested()
    {
        var service = new FakeInstallationActivationStateService(new EnrollmentOutcome(EnrollmentOutcomeStatus.Activated));
        var viewModel = new CredentialRecoveryViewModel(service, NullLogger<CredentialRecoveryViewModel>.Instance);

        var raised = false;
        viewModel.CloseOpenRegisterRequested += (_, _) => raised = true;

        viewModel.CloseOpenRegisterCommand.Execute(null);

        Assert.True(raised);
        Assert.Equal(0, service.RecoverCallCount);
    }

    // Mientras una recuperación está en curso (IsBusy), tampoco debe poder iniciarse el flujo de
    // cierre de caja: ambas acciones comparten la misma pantalla y no deben solaparse.
    [Fact]
    public void CloseOpenRegisterCommandIsDisabledWhileRecoveryIsInProgress()
    {
        var workSource = new TaskCompletionSource<EnrollmentOutcome>();
        var service = new FakeInstallationActivationStateService((_, _) => workSource.Task);
        var viewModel = new CredentialRecoveryViewModel(service, NullLogger<CredentialRecoveryViewModel>.Instance);
        viewModel.RecoveryCode = "ABCD-1234";

        viewModel.ReactivateCommand.Execute(null);

        Assert.False(viewModel.CloseOpenRegisterCommand.CanExecute(null));

        workSource.SetResult(new EnrollmentOutcome(EnrollmentOutcomeStatus.Activated));
    }
}
