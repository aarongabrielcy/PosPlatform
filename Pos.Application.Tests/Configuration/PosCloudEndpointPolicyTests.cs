using Pos.Application.Configuration;

namespace Pos.Application.Tests.Configuration;

// BASIC-REL-01, sección 31: prueba la política de forma completamente determinística, sin ningún
// servidor real. Cubre exactamente los escenarios exigidos por la tarea de release foundation.
public class PosCloudEndpointPolicyTests
{
    [Fact]
    public void DevelopmentWithLocalhostIsAllowed()
    {
        var result = PosCloudEndpointPolicy.Validate("http://localhost:5100", isReleaseBuild: false);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void DevelopmentWithAnyOtherValueIsAlsoAllowed()
    {
        var result = PosCloudEndpointPolicy.Validate("http://192.168.1.50:5100", isReleaseBuild: false);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ReleaseWithHttpsNonLocalhostIsAllowed()
    {
        var result = PosCloudEndpointPolicy.Validate("https://api.posplatform.example.com", isReleaseBuild: true);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("https://localhost:5100")]
    [InlineData("https://127.0.0.1:5100")]
    [InlineData("https://[::1]:5100")]
    public void ReleaseWithLocalhostIsRejected(string baseUrl)
    {
        var result = PosCloudEndpointPolicy.Validate(baseUrl, isReleaseBuild: true);

        Assert.False(result.IsValid);
        Assert.Equal(PosCloudEndpointValidationStatus.LocalhostNotAllowed, result.Status);
    }

    [Fact]
    public void ReleaseWithPlainHttpIsRejected()
    {
        var result = PosCloudEndpointPolicy.Validate("http://api.posplatform.example.com", isReleaseBuild: true);

        Assert.False(result.IsValid);
        Assert.Equal(PosCloudEndpointValidationStatus.RequiresHttps, result.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingEndpointIsRejectedInRelease(string? baseUrl)
    {
        var result = PosCloudEndpointPolicy.Validate(baseUrl, isReleaseBuild: true);

        Assert.False(result.IsValid);
        Assert.Equal(PosCloudEndpointValidationStatus.Missing, result.Status);
    }

    [Fact]
    public void MissingEndpointIsAlsoRejectedInDevelopment()
    {
        // La configuración base (Activation:BaseUrl en appsettings.json) es obligatoria en ambos
        // casos — App.xaml.cs ya falla antes de llegar a esta política si falta por completo (ver
        // "'Activation:BaseUrl' es obligatoria"). Esta prueba documenta que la política en sí no
        // depende de isReleaseBuild para ese caso.
        var result = PosCloudEndpointPolicy.Validate(string.Empty, isReleaseBuild: false);

        Assert.False(result.IsValid);
        Assert.Equal(PosCloudEndpointValidationStatus.Missing, result.Status);
    }

    [Fact]
    public void MalformedUrlIsRejected()
    {
        var result = PosCloudEndpointPolicy.Validate("not a url", isReleaseBuild: true);

        Assert.False(result.IsValid);
        Assert.Equal(PosCloudEndpointValidationStatus.InvalidUrl, result.Status);
    }
}
