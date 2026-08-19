using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Enforcement;
using Pos.Application.InstallationHealth;
using Pos.Desktop.InstallationHealth;
using Pos.Desktop.Tests.Enforcement;

namespace Pos.Desktop.Tests.InstallationHealth;

public class InstallationHeartbeatBackgroundServiceTests
{
    private static readonly TimeSpan TinyInterval = TimeSpan.FromMilliseconds(20);

    [Fact]
    public async Task FirstHeartbeatIsAttemptedPromptlyAfterStarting()
    {
        var sender = new FakeInstallationHeartbeatSender(InstallationHeartbeatSendOutcome.Success);
        var service = CreateService(sender);

        await service.StartAsync(CancellationToken.None);
        try
        {
            await WaitUntilAsync(() => sender.CallCount >= 1);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    // No debe requerirse que la instalación esté activada para que el planificador funcione: una
    // instalación sin activar simplemente recibe NotActivated en cada ciclo y no envía nada (esa
    // decisión vive en InstallationHeartbeatSender - ver InstallationHeartbeatSenderTests). Aquí se
    // comprueba que el planificador sigue funcionando con normalidad en ese caso.
    [Fact]
    public async Task NotActivatedOutcomeDoesNotStopScheduling()
    {
        var sender = new FakeInstallationHeartbeatSender(InstallationHeartbeatSendOutcome.NotActivated);
        var service = CreateService(sender);

        await service.StartAsync(CancellationToken.None);
        try
        {
            await WaitUntilAsync(() => sender.CallCount >= 3);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task PeriodicExecutionOccursAcrossMultipleIntervals()
    {
        var sender = new FakeInstallationHeartbeatSender(InstallationHeartbeatSendOutcome.Success);
        var service = CreateService(sender);

        await service.StartAsync(CancellationToken.None);
        try
        {
            await WaitUntilAsync(() => sender.CallCount >= 3);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task NetworkFailureOutcomeDoesNotStopLaterRetries()
    {
        var sender = new FakeInstallationHeartbeatSender(InstallationHeartbeatSendOutcome.NetworkFailure);
        var service = CreateService(sender);

        await service.StartAsync(CancellationToken.None);
        try
        {
            await WaitUntilAsync(() => sender.CallCount >= 3);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    // Un fallo inesperado (excepción no controlada) en un ciclo tampoco debe detener los ciclos
    // siguientes (ver sección 4/12 de la tarea: el heartbeat nunca es crítico para el POS local).
    [Fact]
    public async Task UnexpectedExceptionInOneCycleDoesNotStopLaterCycles()
    {
        var sender = new FakeInstallationHeartbeatSender(callNumber =>
            callNumber == 1
                ? throw new InvalidOperationException("boom")
                : InstallationHeartbeatSendOutcome.Success);
        var service = CreateService(sender);

        await service.StartAsync(CancellationToken.None);
        try
        {
            await WaitUntilAsync(() => sender.CallCount >= 2);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    // Puente heartbeat -> enforcement (sección 15/17/38 de la tarea): cada resultado confirmado se
    // reenvía a IInstallationEnforcementStateService.ApplyHeartbeatOutcomeAsync, de forma que un
    // estado restrictivo se refleje en la aplicación en ejecución sin esperar un reinicio.
    [Fact]
    public async Task ConfirmedSuspendedOutcomeIsAppliedToTheEnforcementStateService()
    {
        var sender = new FakeInstallationHeartbeatSender(InstallationHeartbeatSendOutcome.Suspended);
        var enforcementStateService = new FakeInstallationEnforcementStateService();
        var service = CreateService(sender, enforcementStateService);

        await service.StartAsync(CancellationToken.None);
        try
        {
            await WaitUntilAsync(() => enforcementStateService.ApplyHeartbeatOutcomeCallCount >= 1);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }

        Assert.Equal(InstallationHeartbeatSendOutcome.Suspended, enforcementStateService.LastAppliedOutcome);
        Assert.Equal(InstallationEnforcementState.Suspended, enforcementStateService.Current);
    }

    [Fact]
    public async Task NetworkFailureOutcomeIsStillForwardedButDoesNotChangeEnforcementState()
    {
        var sender = new FakeInstallationHeartbeatSender(InstallationHeartbeatSendOutcome.NetworkFailure);
        var enforcementStateService = new FakeInstallationEnforcementStateService();
        var service = CreateService(sender, enforcementStateService);

        await service.StartAsync(CancellationToken.None);
        try
        {
            await WaitUntilAsync(() => enforcementStateService.ApplyHeartbeatOutcomeCallCount >= 1);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }

        Assert.Equal(InstallationEnforcementState.Allowed, enforcementStateService.Current);
    }

    [Fact]
    public async Task StoppingTheServiceStopsFurtherScheduling()
    {
        var sender = new FakeInstallationHeartbeatSender(InstallationHeartbeatSendOutcome.Success);
        var service = CreateService(sender);

        await service.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => sender.CallCount >= 1);

        await service.StopAsync(CancellationToken.None);
        var countAfterStop = sender.CallCount;

        await Task.Delay(TinyInterval + TinyInterval);

        Assert.Equal(countAfterStop, sender.CallCount);
    }

    private static InstallationHeartbeatBackgroundService CreateService(
        FakeInstallationHeartbeatSender sender, FakeInstallationEnforcementStateService? enforcementStateService = null) =>
        new(sender, enforcementStateService ?? new FakeInstallationEnforcementStateService(),
            NullLogger<InstallationHeartbeatBackgroundService>.Instance, TinyInterval);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        while (!condition())
        {
            if (timeout.IsCancellationRequested)
            {
                throw new TimeoutException("La condición esperada no se cumplió a tiempo.");
            }

            await Task.Delay(5, CancellationToken.None);
        }
    }
}
