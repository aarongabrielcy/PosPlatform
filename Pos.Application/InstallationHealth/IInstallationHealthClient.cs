namespace Pos.Application.InstallationHealth;

// Puerto hacia pos-cloud para reportar el heartbeat operacional de esta Installation
// (POST api/v1/installation-health/heartbeat). Implementado en Pos.Infrastructure mediante HTTP;
// esta interfaz no debe filtrar detalles de transporte (rutas, encabezados, DTOs HTTP) hacia
// Pos.Application. La credencial se recibe como valor opaco: este puerto no debe registrarla.
public interface IInstallationHealthClient
{
    Task<InstallationHeartbeatClientResult> SendHeartbeatAsync(
        string credential,
        string appVersion,
        DateTimeOffset clientReportedAtUtc,
        CancellationToken cancellationToken);
}
