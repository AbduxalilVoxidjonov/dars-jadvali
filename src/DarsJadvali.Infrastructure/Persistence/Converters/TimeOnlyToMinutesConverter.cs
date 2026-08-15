using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DarsJadvali.Infrastructure.Persistence.Converters;

/// <summary>
/// <see cref="TimeOnly"/> ni yarim tundan boshlab <b>daqiqa</b> (<c>int</c>) sifatida saqlaydi.
/// </summary>
/// <remarks>
/// Ticks (<see cref="TimeSpanToTicksConverter"/>) o'rniga daqiqa tanlandi: baza faylini
/// qo'lda ochganda qiymat o'qilishi mumkin (<c>510</c> = 08:30), va PostgreSQL'ning
/// <c>time</c> turiga ham, SQLite'ga ham arzon ko'chadi.
/// </remarks>
public sealed class TimeOnlyToMinutesConverter : ValueConverter<TimeOnly, int>
{
    public TimeOnlyToMinutesConverter()
        : base(v => v.Hour * 60 + v.Minute,
               v => new TimeOnly(v / 60, v % 60))
    {
    }
}
