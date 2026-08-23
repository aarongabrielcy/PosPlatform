using Pos.Desktop.Common;

namespace Pos.Desktop.Tests.Common;

internal sealed class FakeDesktopClock : IDesktopClock
{
    public FakeDesktopClock(DateTime now)
    {
        Now = now;
    }

    public DateTime Now { get; set; }
}
