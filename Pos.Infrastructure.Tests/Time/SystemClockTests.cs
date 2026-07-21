using Pos.Infrastructure.Time;

namespace Pos.Infrastructure.Tests.Time;

public class SystemClockTests
{
    [Fact]
    public void UtcNowHasZeroOffset()
    {
        var clock = new SystemClock();

        var value = clock.UtcNow;

        Assert.Equal(TimeSpan.Zero, value.Offset);
    }

    [Fact]
    public void UtcNowIsWithinRangeCapturedBeforeAndAfter()
    {
        var clock = new SystemClock();

        var before = DateTimeOffset.UtcNow;
        var value = clock.UtcNow;
        var after = DateTimeOffset.UtcNow;

        Assert.InRange(value, before, after);
    }
}
