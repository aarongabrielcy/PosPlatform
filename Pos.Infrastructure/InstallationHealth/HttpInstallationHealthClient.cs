using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Pos.Application.InstallationHealth;

namespace Pos.Infrastructure.InstallationHealth;

// Implementa IInstallationHealthClient contra el endpoint real de pos-cloud (develop):
// POST api/v1/installation-health/heartbeat. No debe registrar nunca la Installation Credential
// (ver sección 9/30 de la tarea) — únicamente códigos de estado HTTP y códigos de error del cuerpo
// de respuesta.
public sealed partial class HttpInstallationHealthClient : IInstallationHealthClient
{
    private const string HeartbeatPath = "api/v1/installation-health/heartbeat";
    private const string InstallationSuspendedCode = "INSTALLATION_SUSPENDED";
    private const string InstallationDecommissionedCode = "INSTALLATION_DECOMMISSIONED";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpInstallationHealthClient> _logger;

    public HttpInstallationHealthClient(HttpClient httpClient, ILogger<HttpInstallationHealthClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<InstallationHeartbeatClientResult> SendHeartbeatAsync(
        string credential,
        string appVersion,
        DateTimeOffset clientReportedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(credential);
        ArgumentException.ThrowIfNullOrWhiteSpace(appVersion);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, HeartbeatPath)
            {
                Content = JsonContent.Create(
                    new RecordHeartbeatHttpRequest(appVersion, FormatClientReportedAt(clientReportedAtUtc)),
                    options: SerializerOptions),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential);

            using var httpResponse = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (httpResponse.StatusCode == HttpStatusCode.NoContent)
            {
                LogHeartbeatSucceeded(_logger);
                return InstallationHeartbeatClientResult.Success();
            }

            if (httpResponse.StatusCode == HttpStatusCode.Unauthorized)
            {
                LogCredentialInvalid(_logger);
                return InstallationHeartbeatClientResult.CredentialInvalid();
            }

            if (httpResponse.StatusCode == HttpStatusCode.Forbidden)
            {
                return await ClassifyForbiddenAsync(httpResponse, cancellationToken).ConfigureAwait(false);
            }

            LogUnexpectedStatusCode(_logger, (int)httpResponse.StatusCode);
            return InstallationHeartbeatClientResult.NetworkFailure();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            LogHeartbeatRequestFailed(_logger, ex);
            return InstallationHeartbeatClientResult.NetworkFailure();
        }
    }

    private async Task<InstallationHeartbeatClientResult> ClassifyForbiddenAsync(
        HttpResponseMessage httpResponse,
        CancellationToken cancellationToken)
    {
        var errorCode = await TryReadErrorCodeAsync(httpResponse, cancellationToken).ConfigureAwait(false);

        switch (errorCode)
        {
            case InstallationSuspendedCode:
                LogInstallationSuspended(_logger);
                return InstallationHeartbeatClientResult.Suspended();

            case InstallationDecommissionedCode:
                LogInstallationDecommissioned(_logger);
                return InstallationHeartbeatClientResult.Decommissioned();

            default:
                LogUnexpectedStatusCode(_logger, (int)httpResponse.StatusCode);
                return InstallationHeartbeatClientResult.NetworkFailure();
        }
    }

    private static async Task<string?> TryReadErrorCodeAsync(HttpResponseMessage httpResponse, CancellationToken cancellationToken)
    {
        try
        {
            var body = await httpResponse.Content
                .ReadFromJsonAsync<HeartbeatErrorHttpResponse>(SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
            return body?.Code;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // Formato exacto usado por el ejemplo del contrato real (record-heartbeat.request.dto.ts /
    // installation-health.md#heartbeat-contract): "2026-08-12T23:10:00.000Z". Se construye a mano
    // en vez de usar "O" (round-trip) para no depender de que DateTimeOffset ya esté normalizado a
    // UTC ni arrastrar dígitos de fracción de segundo fuera de milisegundos.
    private static string FormatClientReportedAt(DateTimeOffset clientReportedAtUtc) =>
        clientReportedAtUtc.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Heartbeat de instalación enviado correctamente.")]
    private static partial void LogHeartbeatSucceeded(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "El servidor rechazó la credencial de instalación (HTTP 401 INSTALLATION_CREDENTIAL_INVALID).")]
    private static partial void LogCredentialInvalid(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "La instalación está suspendida (HTTP 403 INSTALLATION_SUSPENDED).")]
    private static partial void LogInstallationSuspended(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "La instalación está decomisionada (HTTP 403 INSTALLATION_DECOMMISSIONED).")]
    private static partial void LogInstallationDecommissioned(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "El servidor de heartbeat devolvió un estado inesperado (HTTP {StatusCode}).")]
    private static partial void LogUnexpectedStatusCode(ILogger logger, int statusCode);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Fallo de red al intentar contactar el servidor de heartbeat.")]
    private static partial void LogHeartbeatRequestFailed(ILogger logger, Exception exception);
}
