using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pos.Application.Enforcement;
using Pos.Application.InstallationHealth;

namespace Pos.Desktop.InstallationHealth;

// Planifica el heartbeat operacional de esta Installation en segundo plano (ver sección 6/16/17 de
// la tarea). BackgroundService.StartAsync() no espera a que ExecuteAsync termine —solo a que
// arranque—, así que host.Start() en App.xaml.cs nunca bloquea el arranque de la UI esperando un
// heartbeat exitoso.
//
// El planificador no distingue "instalación no activada" de "instalación activada": simplemente
// intenta un heartbeat en cada ciclo y deja que IInstallationHeartbeatSender decida si corresponde
// enviarlo (ver InstallationHeartbeatSender.SendHeartbeatAsync). Esto evita el "hack de estado
// manual frágil" que la sección 18 de la tarea pide evitar: una instalación recién activada queda
// elegible para el heartbeat en el siguiente ciclo sin reiniciar el proceso ni requerir que
// App.xaml.cs arranque/detenga este servicio explícitamente.
public sealed partial class InstallationHeartbeatBackgroundService : BackgroundService
{
    // Cadencia nominal documentada por el backend (INSTALLATION_HEARTBEAT_INTERVAL_SECONDS,
    // valor por defecto — ver installation-health-env.schema.ts en pos-cloud). El servidor no la
    // exige ni la devuelve; el Desktop debe usarla igual como intervalo fijo (ver sección 6/22 de
    // la tarea: no inventar un intervalo más agresivo, la limitación de tasa del backend es
    // backlog conocido y no se resuelve aquí).
    internal static readonly TimeSpan DefaultHeartbeatInterval = TimeSpan.FromSeconds(60);

    private readonly IInstallationHeartbeatSender _sender;
    private readonly IInstallationEnforcementStateService _enforcementStateService;
    private readonly IInstallationConnectivityStateService _connectivityStateService;
    private readonly ILogger<InstallationHeartbeatBackgroundService> _logger;
    private readonly TimeSpan _heartbeatInterval;

    public InstallationHeartbeatBackgroundService(
        IInstallationHeartbeatSender sender,
        IInstallationEnforcementStateService enforcementStateService,
        IInstallationConnectivityStateService connectivityStateService,
        ILogger<InstallationHeartbeatBackgroundService> logger)
        : this(sender, enforcementStateService, connectivityStateService, logger, DefaultHeartbeatInterval)
    {
    }

    // Constructor interno: permite a las pruebas usar un intervalo mínimo en vez de esperar 60
    // segundos reales (ver sección 25 de la tarea). Pos.Desktop expone InternalsVisibleTo a
    // Pos.Desktop.Tests.
    internal InstallationHeartbeatBackgroundService(
        IInstallationHeartbeatSender sender,
        IInstallationEnforcementStateService enforcementStateService,
        IInstallationConnectivityStateService connectivityStateService,
        ILogger<InstallationHeartbeatBackgroundService> logger,
        TimeSpan heartbeatInterval)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _enforcementStateService = enforcementStateService ?? throw new ArgumentNullException(nameof(enforcementStateService));
        _connectivityStateService = connectivityStateService ?? throw new ArgumentNullException(nameof(connectivityStateService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _heartbeatInterval = heartbeatInterval;
    }

    // Primer heartbeat inmediato (para que el Control Plane no espere un intervalo completo antes
    // de mostrar la instalación — sección 6), seguido de intentos periódicos hasta que el
    // CancellationToken se cancele (apagado del Host — ver App.xaml.cs OnExit). Un fallo de red o
    // una excepción inesperada en un ciclo nunca detiene el planificador: el siguiente ciclo
    // simplemente vuelve a intentarlo.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_heartbeatInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            await SendOnceAsync(stoppingToken).ConfigureAwait(false);

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task SendOnceAsync(CancellationToken cancellationToken)
    {
        // Instrumentación INST-ENF-02 (sección 7 de la tarea de release foundation): duración real
        // del intento y transiciones de enforcement/conectividad, para poder diagnosticar una
        // recuperación lenta sin adivinar. Nunca registra la Installation Credential ni encabezados
        // de autorización (sección 9/18/19) — solo el resultado clasificado y la duración.
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        LogHeartbeatAttemptStarted(_logger);

        var previousEnforcement = _enforcementStateService.Current;
        var previousConnectivity = _connectivityStateService.Current;

        try
        {
            var outcome = await _sender.SendHeartbeatAsync(cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            LogHeartbeatCompleted(_logger, outcome.ToString(), stopwatch.ElapsedMilliseconds);
            LogOutcome(outcome);

            // Puente heartbeat -> enforcement (sección 15/16 de la tarea): el estado de enforcement
            // en memoria debe reflejar un resultado confirmado sin esperar a un reinicio del proceso.
            await _enforcementStateService.ApplyHeartbeatOutcomeAsync(outcome, cancellationToken).ConfigureAwait(false);

            var newEnforcement = _enforcementStateService.Current;
            if (newEnforcement != previousEnforcement)
            {
                LogEnforcementTransition(_logger, previousEnforcement.ToString(), newEnforcement.ToString());
            }

            // Puente heartbeat -> indicador de conectividad POS Cloud (BASIC-UX-01, sección 30):
            // reutiliza el mismo resultado de heartbeat, sin ningún poll HTTP adicional.
            _connectivityStateService.ApplyHeartbeatOutcome(outcome);

            var newConnectivity = _connectivityStateService.Current;
            if (newConnectivity != previousConnectivity)
            {
                LogConnectivityTransition(_logger, previousConnectivity.ToString(), newConnectivity.ToString());
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // El heartbeat es telemetría operacional, nunca crítica para el POS local (ver sección
            // 4/12 de la tarea): un fallo inesperado aquí no debe derribar el BackgroundService ni
            // la aplicación; simplemente se reintenta en el próximo ciclo.
            LogUnexpectedFailure(_logger, ex);
        }
    }

    private void LogOutcome(InstallationHeartbeatSendOutcome outcome)
    {
        switch (outcome)
        {
            case InstallationHeartbeatSendOutcome.Success:
                LogHeartbeatSucceeded(_logger);
                break;
            case InstallationHeartbeatSendOutcome.NotActivated:
                LogNotActivated(_logger);
                break;
            case InstallationHeartbeatSendOutcome.CredentialMissing:
                LogCredentialMissing(_logger);
                break;
            case InstallationHeartbeatSendOutcome.CredentialInvalid:
                LogCredentialInvalid(_logger);
                break;
            case InstallationHeartbeatSendOutcome.Suspended:
                LogSuspended(_logger);
                break;
            case InstallationHeartbeatSendOutcome.Decommissioned:
                LogDecommissioned(_logger);
                break;
            case InstallationHeartbeatSendOutcome.NetworkFailure:
                LogNetworkFailure(_logger);
                break;
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Intento de heartbeat de instalación iniciado.")]
    private static partial void LogHeartbeatAttemptStarted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Heartbeat de instalación completado. Resultado={Outcome}, duración={ElapsedMilliseconds}ms.")]
    private static partial void LogHeartbeatCompleted(ILogger logger, string outcome, long elapsedMilliseconds);

    [LoggerMessage(Level = LogLevel.Information, Message = "Transición de estado de enforcement: {PreviousState} -> {NewState}.")]
    private static partial void LogEnforcementTransition(ILogger logger, string previousState, string newState);

    [LoggerMessage(Level = LogLevel.Information, Message = "Transición de conectividad con POS Cloud: {PreviousState} -> {NewState}.")]
    private static partial void LogConnectivityTransition(ILogger logger, string previousState, string newState);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Heartbeat de instalación enviado correctamente.")]
    private static partial void LogHeartbeatSucceeded(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Heartbeat omitido: la instalación aún no está activada.")]
    private static partial void LogNotActivated(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Heartbeat omitido: hay metadatos de activación local pero no se encontró la credencial de instalación.")]
    private static partial void LogCredentialMissing(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "El servidor rechazó la credencial de instalación (INSTALLATION_CREDENTIAL_INVALID).")]
    private static partial void LogCredentialInvalid(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "La instalación está suspendida (INSTALLATION_SUSPENDED).")]
    private static partial void LogSuspended(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "La instalación está decomisionada (INSTALLATION_DECOMMISSIONED).")]
    private static partial void LogDecommissioned(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "No fue posible enviar el heartbeat por un fallo de red; se reintentará en el próximo ciclo.")]
    private static partial void LogNetworkFailure(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Fallo inesperado al enviar el heartbeat de instalación.")]
    private static partial void LogUnexpectedFailure(ILogger logger, Exception exception);
}
