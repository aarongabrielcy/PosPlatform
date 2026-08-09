using Pos.Application.Common.Time;

namespace Pos.Desktop.Tests.Sales.History;

internal sealed class FakeClock : IClock
{
    public FakeClock(DateTimeOffset utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTimeOffset UtcNow { get; }
}
