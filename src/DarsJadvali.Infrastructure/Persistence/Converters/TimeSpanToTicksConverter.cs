using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DarsJadvali.Infrastructure.Persistence.Converters;

/// <summary>
/// SQLite <see cref="TimeSpan"/> turini qo'llab-quvvatlamaydi — shuning uchun
/// qiymatlar <c>long</c> (ticks) sifatida saqlanadi.
/// </summary>
public sealed class TimeSpanToTicksConverter : ValueConverter<TimeSpan, long>
{
    public TimeSpanToTicksConverter()
        : base(v => v.Ticks, v => TimeSpan.FromTicks(v))
    {
    }
}

/// <summary>Nullable <see cref="TimeSpan"/> uchun ticks konverteri.</summary>
public sealed class NullableTimeSpanToTicksConverter : ValueConverter<TimeSpan?, long?>
{
    public NullableTimeSpanToTicksConverter()
        : base(v => v.HasValue ? v.Value.Ticks : (long?)null,
               v => v.HasValue ? TimeSpan.FromTicks(v.Value) : (TimeSpan?)null)
    {
    }
}
