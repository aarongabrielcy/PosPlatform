using System.Net;
using System.Net.Http.Headers;
using Pos.Application.InstallationHealth;
using Pos.Infrastructure.InstallationHealth;
using Pos.Infrastructure.Tests.Activation;

namespace Pos.Infrastructure.Tests.InstallationHealth;

// Contrato real de pos-cloud (develop), verificado por auditoría de fuente:
// POST api/v1/installation-health/heartbeat - Authorization: Bearer <credential>
// Request:  { "appVersion": string, "clientReportedAt"?: string }
// 204 No Content
// 401 { statusCode, code: "INSTALLATION_CREDENTIAL_INVALID", ... }
// 403 { statusCode, code: "INSTALLATION_SUSPENDED" | "INSTALLATION_DECOMMISSIONED", ... }
public class HttpInstallationHealthClientTests
{
    private const string Credential = "cred-1.super-secret-credential-value";
    private const string ExpectedPath = "http://localhost/api/v1/installation-health/heartbeat";
    private static readonly DateTimeOffset ClientReportedAtUtc = new(2026, 8, 12, 23, 10, 0, TimeSpan.Zero);

    [Fact]
    public async Task SuccessfulHeartbeatPostsExactlyOnceWithBearerCredentialAndExpectedBody()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(ExpectedPath, request.RequestUri!.ToString());
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal(Credential, request.Headers.Authorization!.Parameter);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        });

        var (client, logger) = CreateClient(handler);

        var result = await client.SendHeartbeatAsync(Credential, "1.4.2", ClientReportedAtUtc, CancellationToken.None);

        Assert.Equal(InstallationHeartbeatClientStatus.Success, result.Status);
        Assert.Equal(1, handler.RequestCount);
        Assert.Contains("\"appVersion\":\"1.4.2\"", handler.LastRequestBody);
        Assert.Contains("\"clientReportedAt\":\"2026-08-12T23:10:00.000Z\"", handler.LastRequestBody);
        AssertNoSensitiveValuesLogged(logger);
    }

    [Fact]
    public async Task NoAuthorizationHeaderIsNeverSentWithoutACredential()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            Assert.NotNull(request.Headers.Authorization);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        });

        var (client, _) = CreateClient(handler);

        await client.SendHeartbeatAsync(Credential, "1.4.2", ClientReportedAtUtc, CancellationToken.None);

        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task Returns401MapsToCredentialInvalid()
    {
        var handler = new StubHttpMessageHandler(_ => Task.FromResult(JsonResponse(
            HttpStatusCode.Unauthorized,
            """{"statusCode":401,"code":"INSTALLATION_CREDENTIAL_INVALID","message":"Invalid installation credential.","correlationId":"abc"}""")));

        var (client, logger) = CreateClient(handler);

        var result = await client.SendHeartbeatAsync(Credential, "1.4.2", ClientReportedAtUtc, CancellationToken.None);

        Assert.Equal(InstallationHeartbeatClientStatus.CredentialInvalid, result.Status);
        AssertNoSensitiveValuesLogged(logger);
    }

    [Fact]
    public async Task Returns403WithSuspendedCodeMapsToSuspended()
    {
        var handler = new StubHttpMessageHandler(_ => Task.FromResult(JsonResponse(
            HttpStatusCode.Forbidden,
            """{"statusCode":403,"code":"INSTALLATION_SUSPENDED","message":"This installation is currently suspended.","correlationId":"abc"}""")));

        var (client, logger) = CreateClient(handler);

        var result = await client.SendHeartbeatAsync(Credential, "1.4.2", ClientReportedAtUtc, CancellationToken.None);

        Assert.Equal(InstallationHeartbeatClientStatus.Suspended, result.Status);
        AssertNoSensitiveValuesLogged(logger);
    }

    [Fact]
    public async Task Returns403WithDecommissionedCodeMapsToDecommissioned()
    {
        var handler = new StubHttpMessageHandler(_ => Task.FromResult(JsonResponse(
            HttpStatusCode.Forbidden,
            """{"statusCode":403,"code":"INSTALLATION_DECOMMISSIONED","message":"This installation has been decommissioned.","correlationId":"abc"}""")));

        var (client, logger) = CreateClient(handler);

        var result = await client.SendHeartbeatAsync(Credential, "1.4.2", ClientReportedAtUtc, CancellationToken.None);

        Assert.Equal(InstallationHeartbeatClientStatus.Decommissioned, result.Status);
        AssertNoSensitiveValuesLogged(logger);
    }

    [Fact]
    public async Task ServerErrorMapsToNetworkFailure()
    {
        var handler = new StubHttpMessageHandler(_ => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        var (client, logger) = CreateClient(handler);

        var result = await client.SendHeartbeatAsync(Credential, "1.4.2", ClientReportedAtUtc, CancellationToken.None);

        Assert.Equal(InstallationHeartbeatClientStatus.NetworkFailure, result.Status);
        AssertNoSensitiveValuesLogged(logger);
    }

    [Fact]
    public async Task TransportExceptionMapsToNetworkFailure()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("DNS failure"));

        var (client, logger) = CreateClient(handler);

        var result = await client.SendHeartbeatAsync(Credential, "1.4.2", ClientReportedAtUtc, CancellationToken.None);

        Assert.Equal(InstallationHeartbeatClientStatus.NetworkFailure, result.Status);
        AssertNoSensitiveValuesLogged(logger);
    }

    // INST-ENF-02 (BASIC-REL-01, sección 6/9): regresión del mecanismo detrás de la corrección de
    // DependencyInjection.HttpClientTimeout. Antes de acotar HttpClient.Timeout explícitamente, una
    // petición colgada bloqueaba hasta el valor por defecto de 100s, empujando directamente el
    // siguiente ciclo de heartbeat fuera del intervalo nominal de ~60s (ver
    // InstallationHeartbeatBackgroundService, que encadena "enviar -> esperar el intervalo" sin
    // ejecutar heartbeats en paralelo consigo mismo). Este test prueba que, con un Timeout acotado,
    // una petición que nunca respondería por sí sola se clasifica como NetworkFailure con rapidez en
    // vez de bloquear indefinidamente — la misma mecánica de la que depende la corrección real en
    // Pos.Infrastructure.DependencyInjection.
    [Fact]
    public async Task RequestThatHangsBeyondTheConfiguredTimeoutIsClassifiedAsNetworkFailureWithoutBlockingIndefinitely()
    {
        var httpClient = new HttpClient(new HangingHttpMessageHandler())
        {
            BaseAddress = new Uri("http://localhost/"),
            Timeout = TimeSpan.FromMilliseconds(200),
        };
        var logger = new CapturingLogger<HttpInstallationHealthClient>();
        var client = new HttpInstallationHealthClient(httpClient, logger);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await client.SendHeartbeatAsync(Credential, "1.4.2", ClientReportedAtUtc, CancellationToken.None);
        stopwatch.Stop();

        Assert.Equal(InstallationHeartbeatClientStatus.NetworkFailure, result.Status);

        // Margen generoso (bien por debajo del Timeout por defecto de 100s que causaba el síntoma
        // original) para no volverse intermitente bajo contención de thread pool cuando la suite
        // completa corre en paralelo — el punto de esta prueba es "acotado", no precisión exacta.
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(30),
            $"Se esperaba que la petición fallara muy por debajo del Timeout por defecto de 100s, pero tardó {stopwatch.Elapsed}.");
    }

    // A diferencia de StubHttpMessageHandler (que no reenvía el CancellationToken real al delegado
    // de la prueba), este handler dedicado sí lo observa — necesario para que HttpClient.Timeout
    // pueda cancelar la petición colgada en vez de bloquear indefinidamente.
    private sealed class HangingHttpMessageHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json),
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return response;
    }

    private static void AssertNoSensitiveValuesLogged(CapturingLogger<HttpInstallationHealthClient> logger)
    {
        foreach (var message in logger.Messages)
        {
            Assert.DoesNotContain(Credential, message, StringComparison.Ordinal);
        }
    }

    private static (HttpInstallationHealthClient Client, CapturingLogger<HttpInstallationHealthClient> Logger) CreateClient(
        StubHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var logger = new CapturingLogger<HttpInstallationHealthClient>();
        return (new HttpInstallationHealthClient(httpClient, logger), logger);
    }
}
