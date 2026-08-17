using Pos.Application.Common.Versioning;

namespace Pos.Application.Tests.InstallationHealth;

internal sealed class FakeApplicationVersionProvider : IApplicationVersionProvider
{
    private readonly string _version;

    public FakeApplicationVersionProvider(string version)
    {
        _version = version;
    }

    public string GetVersion() => _version;
}
