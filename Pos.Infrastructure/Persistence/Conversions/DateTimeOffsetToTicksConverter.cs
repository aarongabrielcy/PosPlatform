using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Pos.Infrastructure.Persistence.Conversions;

internal sealed class DateTimeOffsetToTicksConverter : ValueConverter<DateTimeOffset, long>
{
    public DateTimeOffsetToTicksConverter()
        : base(
            value => value.UtcTicks,
            value => new DateTimeOffset(value, TimeSpan.Zero))
    {
    }
}
