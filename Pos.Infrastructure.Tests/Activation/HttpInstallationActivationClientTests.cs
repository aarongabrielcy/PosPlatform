using System.Net;
using System.Net.Http.Headers;
using Pos.Application.Activation;
using Pos.Infrastructure.Activation;

namespace Pos.Infrastructure.Tests.Activation;

// Contrato real de pos-cloud (develop), verificado por auditoría de fuente:
// POST api/v1/installation-auth/enroll — sin autenticación
// 201 { "installationId": string, "credential": string }
// 401 { statusCode, code: "ENROLLMENT_FAILED", ... } (código inválido/expirado/usado/desconocido)
public class HttpInstallationActivationClientTests
{
    private const string EnrollmentCode = "abc123.super-secret-enrollment-code";
    private const string ExpectedPath = "http://localhost/api/v1/installation-auth/enroll";

    [Fact]
    public async Task SuccessfulEnrollmentPostsExactlyOnceAndReturnsInstallationIdAndCredential()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(ExpectedPath, request.RequestUri!.ToString());
            Assert.Null(request.Headers.Authorization);

            return Task.FromResult(JsonResponse(
                HttpStatusCode.Created,
                """{"installationId":"inst-1","credential":"cred-1.secret"}"""));
        });

        var (client, logger) = CreateClient(handler);

        var result = await client.EnrollAsync(EnrollmentCode, CancellationToken.None);

        Assert.Equal(InstallationEnrollmentClientStatus.Success, result.Status);
        Assert.Equal("inst-1", result.InstallationId);
        Assert.Equal("cred-1.secret", result.Credential);
        Assert.Equal(1, handler.RequestCount);
        Assert.Contains($"\"enrollmentCode\":\"{EnrollmentCode}\"", handler.LastRequestBody);
        AssertNoSensitiveValuesLogged(logger);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task RejectedStatusCodesMapToRejectedWithoutLeakingDetails(HttpStatusCode statusCode)
    {
        var handler = new StubHttpMessageHandler(_ => Task.FromResult(JsonResponse(
            statusCode,
            """{"statusCode":401,"code":"ENROLLMENT_FAILED","message":"Enrollment failed.","correlationId":"abc"}""")));

        var (client, logger) = CreateClient(handler);

        var result = await client.EnrollAsync(EnrollmentCode, CancellationToken.None);

        Assert.Equal(InstallationEnrollmentClientStatus.Rejected, result.Status);
        Assert.Null(result.InstallationId);
        Assert.Null(result.Credential);
        AssertNoSensitiveValuesLogged(logger);
    }

    [Fact]
    public async Task ServerErrorMapsToNetworkFailure()
    {
        var handler = new StubHttpMessageHandler(_ => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        var (client, logger) = CreateClient(handler);

        var result = await client.EnrollAsync(EnrollmentCode, CancellationToken.None);

        Assert.Equal(InstallationEnrollmentClientStatus.NetworkFailure, result.Status);
        AssertNoSensitiveValuesLogged(logger);
    }

    [Fact]
    public async Task TransportExceptionMapsToNetworkFailure()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("DNS failure"));

        var (client, logger) = CreateClient(handler);

        var result = await client.EnrollAsync(EnrollmentCode, CancellationToken.None);

        Assert.Equal(InstallationEnrollmentClientStatus.NetworkFailure, result.Status);
        AssertNoSensitiveValuesLogged(logger);
    }

    [Fact]
    public async Task MalformedSuccessBodyMapsToNetworkFailureInsteadOfThrowing()
    {
        var handler = new StubHttpMessageHandler(_ => Task.FromResult(
            JsonResponse(HttpStatusCode.Created, """{"installationId":""}""")));

        var (client, _) = CreateClient(handler);

        var result = await client.EnrollAsync(EnrollmentCode, CancellationToken.None);

        Assert.Equal(InstallationEnrollmentClientStatus.NetworkFailure, result.Status);
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

    private static void AssertNoSensitiveValuesLogged(CapturingLogger<HttpInstallationActivationClient> logger)
    {
        foreach (var message in logger.Messages)
        {
            Assert.DoesNotContain(EnrollmentCode, message, StringComparison.Ordinal);
            Assert.DoesNotContain("cred-1.secret", message, StringComparison.Ordinal);
        }
    }

    private static (HttpInstallationActivationClient Client, CapturingLogger<HttpInstallationActivationClient> Logger) CreateClient(
        StubHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var logger = new CapturingLogger<HttpInstallationActivationClient>();
        return (new HttpInstallationActivationClient(httpClient, logger), logger);
    }
}
