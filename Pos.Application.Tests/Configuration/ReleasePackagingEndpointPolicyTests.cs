using Pos.Application.Configuration;

namespace Pos.Application.Tests.Configuration;

// BASIC-INS-01, sección 48/49: cubre exactamente la matriz exigida por la tarea de empaquetado del
// instalador — missing/http/localhost/reserved-placeholder fallan, HTTPS real-looking pasa, y el
// marcador de posición reservado solo se permite en modo TEST explícito.
public class ReleasePackagingEndpointPolicyTests
{
    [Fact]
    public void FinalBuildWithRealLookingHttpsHostIsAllowed()
    {
        var result = ReleasePackagingEndpointPolicy.Validate("https://api.posplatform.com", isTestBuild: false);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingEndpointFailsInFinalMode(string? baseUrl)
    {
        var result = ReleasePackagingEndpointPolicy.Validate(baseUrl, isTestBuild: false);

        Assert.False(result.IsValid);
        Assert.Equal(ReleasePackagingEndpointValidationStatus.Missing, result.Status);
    }

    [Fact]
    public void MissingEndpointFailsInTestModeToo()
    {
        var result = ReleasePackagingEndpointPolicy.Validate(null, isTestBuild: true);

        Assert.False(result.IsValid);
        Assert.Equal(ReleasePackagingEndpointValidationStatus.Missing, result.Status);
    }

    [Fact]
    public void MalformedUrlFailsInFinalMode()
    {
        var result = ReleasePackagingEndpointPolicy.Validate("not a url", isTestBuild: false);

        Assert.False(result.IsValid);
        Assert.Equal(ReleasePackagingEndpointValidationStatus.InvalidUrl, result.Status);
    }

    [Fact]
    public void PlainHttpFailsEvenInTestMode()
    {
        var result = ReleasePackagingEndpointPolicy.Validate("http://posplatform-release-test.invalid", isTestBuild: true);

        Assert.False(result.IsValid);
        Assert.Equal(ReleasePackagingEndpointValidationStatus.RequiresHttps, result.Status);
    }

    [Theory]
    [InlineData("https://localhost:5100")]
    [InlineData("https://127.0.0.1:5100")]
    [InlineData("https://[::1]:5100")]
    public void LocalhostFailsEvenInTestMode(string baseUrl)
    {
        var result = ReleasePackagingEndpointPolicy.Validate(baseUrl, isTestBuild: true);

        Assert.False(result.IsValid);
        Assert.Equal(ReleasePackagingEndpointValidationStatus.LocalhostNotAllowed, result.Status);
    }

    [Theory]
    [InlineData("https://posplatform-release-test.invalid")]
    [InlineData("https://staging.posplatform.test")]
    [InlineData("https://demo.posplatform.example")]
    public void ReservedPlaceholderDomainFailsInFinalMode(string baseUrl)
    {
        var result = ReleasePackagingEndpointPolicy.Validate(baseUrl, isTestBuild: false);

        Assert.False(result.IsValid);
        Assert.Equal(ReleasePackagingEndpointValidationStatus.ReservedPlaceholderNotAllowed, result.Status);
    }

    [Theory]
    [InlineData("https://posplatform-release-test.invalid")]
    [InlineData("https://staging.posplatform.test")]
    [InlineData("https://demo.posplatform.example")]
    public void ReservedPlaceholderDomainIsAllowedOnlyInTestMode(string baseUrl)
    {
        var result = ReleasePackagingEndpointPolicy.Validate(baseUrl, isTestBuild: true);

        Assert.True(result.IsValid);
    }
}
