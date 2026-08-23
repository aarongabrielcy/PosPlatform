using Pos.Application.Common.Versioning;

namespace Pos.Desktop.Tests.LocalConfiguration;

internal sealed class FakeApplicationVersionProvider : IApplicationVersionProvider
{
    public string Version { get; set; } = "1.0.0";

    public string GetVersion() => Version;
}
