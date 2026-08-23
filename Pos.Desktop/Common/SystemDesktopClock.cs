namespace Pos.Desktop.Common;

public sealed class SystemDesktopClock : IDesktopClock
{
    public DateTime Now => DateTime.Now;
}
