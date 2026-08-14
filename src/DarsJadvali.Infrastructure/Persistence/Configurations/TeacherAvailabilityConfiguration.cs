using DarsJadvali.Domain.Entities;
using DarsJadvali.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DarsJadvali.Infrastructure.Persistence.Configurations;

public sealed class TeacherAvailabilityConfiguration : IEntityTypeConfiguration<TeacherAvailability>
{
    public void Configure(EntityTypeBuilder<TeacherAvailability> builder)
    {
        builder.ToTable("TeacherAvailabilities");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.DayOfWeek)
            .IsRequired()
            .HasConversion<int>();

        // SQLite TimeSpan'ni bilmaydi — ticks (long) sifatida saqlanadi.
        builder.Property(x => x.StartTime)
            .IsRequired()
            .HasConversion(new TimeSpanToTicksConverter());

        builder.Property(x => x.EndTime)
            .IsRequired()
            .HasConversion(new TimeSpanToTicksConverter());

        builder.Property(x => x.IsAvailable)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasOne(x => x.Teacher)
            .WithMany(t => t.Availabilities)
            .HasForeignKey(x => x.TeacherId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.TeacherId, x.DayOfWeek });

        builder.Navigation(x => x.Teacher).AutoInclude();
    }
}
