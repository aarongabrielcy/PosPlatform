namespace Pos.Application.Common.Time;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
