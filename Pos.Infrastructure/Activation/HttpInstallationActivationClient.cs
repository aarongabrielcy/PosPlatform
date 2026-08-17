using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Pos.Application.Activation;

namespace Pos.Infrastructure.Activation;

// Implementa IInstallationActivationClient contra el endpoint real de pos-cloud (develop):
// POST api/v1/installation-auth/enroll. No debe registrar nunca el Enrollment Code ni la
// Installation Credential (ver sección 30 de la tarea) — únicamente códigos de estado HTTP.
public sealed partial class HttpInstallationActivationClient : IInstallationActivationClient
{
    private const string EnrollPath = "api/v1/installation-auth/enroll";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpInstallationActivationClient> _logger;

    public HttpInstallationActivationClient(HttpClient httpClient, ILogger<HttpInstallationActivationClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<InstallationEnrollmentClientResult> EnrollAsync(string enrollmentCode, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(enrollmentCode);

        try
        {
            using var httpResponse = await _httpClient
                .PostAsJsonAsync(EnrollPath, new EnrollInstallationHttpRequest(enrollmentCode), SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            if (httpResponse.StatusCode == HttpStatusCode.Created)
            {
                return await ParseSuccessResponseAsync(httpResponse, cancellationToken).ConfigureAwait(false);
            }

            if (httpResponse.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
            {
                LogEnrollmentRejected(_logger, (int)httpResponse.StatusCode);
                return InstallationEnrollmentClientResult.Rejected();
            }

            LogUnexpectedStatusCode(_logger, (int)httpResponse.StatusCode);
            return InstallationEnrollmentClientResult.NetworkFailure();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            LogEnrollmentRequestFailed(_logger, ex);
            return InstallationEnrollmentClientResult.NetworkFailure();
        }
    }

    private async Task<InstallationEnrollmentClientResult> ParseSuccessResponseAsync(
        HttpResponseMessage httpResponse,
        CancellationToken cancellationToken)
    {
        var body = await httpResponse.Content
            .ReadFromJsonAsync<EnrollInstallationHttpResponse>(SerializerOptions, cancellationToken)
            .ConfigureAwait(false);

        if (body is null || string.IsNullOrWhiteSpace(body.InstallationId) || string.IsNullOrWhiteSpace(body.Credential))
        {
            LogUnexpectedResponseShape(_logger);
            return InstallationEnrollmentClientResult.NetworkFailure();
        }

        return InstallationEnrollmentClientResult.Success(body.InstallationId, body.Credential);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "El servidor de activación rechazó el código (HTTP {StatusCode}).")]
    private static partial void LogEnrollmentRejected(ILogger logger, int statusCode);

    [LoggerMessage(Level = LogLevel.Error, Message = "El servidor de activación devolvió un estado inesperado (HTTP {StatusCode}).")]
    private static partial void LogUnexpectedStatusCode(ILogger logger, int statusCode);

    [LoggerMessage(Level = LogLevel.Error, Message = "El servidor de activación devolvió una respuesta 201 con forma inesperada.")]
    private static partial void LogUnexpectedResponseShape(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Fallo de red al intentar contactar el servidor de activación.")]
    private static partial void LogEnrollmentRequestFailed(ILogger logger, Exception exception);
}
