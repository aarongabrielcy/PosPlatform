namespace Pos.Application.InstallationHealth;

// Orquesta un único intento de heartbeat: decide si esta Installation es elegible (activada y con
// credencial local disponible) y, de serlo, lo reporta a pos-cloud. No decide cadencia ni
// reintentos: eso es responsabilidad del planificador en Pos.Desktop (ver sección 10/16 de la
// tarea).
public interface IInstallationHeartbeatSender
{
    Task<InstallationHeartbeatSendOutcome> SendHeartbeatAsync(CancellationToken cancellationToken);
}
