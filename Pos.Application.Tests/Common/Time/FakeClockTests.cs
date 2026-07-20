namespace Pos.Application.Tests.Common.Time;

public class FakeClockTests
{
    [Fact]
    public void ProvidesFixedUtcDateTimeOffset()
    {
        var fixedUtcNow = new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);
        var clock = new FakeClock(fixedUtcNow);

        Assert.Equal(fixedUtcNow, clock.UtcNow);
        Assert.Equal(TimeSpan.Zero, clock.UtcNow.Offset);
    }
}
