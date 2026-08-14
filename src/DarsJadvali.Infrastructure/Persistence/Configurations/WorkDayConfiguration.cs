using DarsJadvali.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DarsJadvali.Infrastructure.Persistence.Configurations;

public sealed class WorkDayConfiguration : IEntityTypeConfiguration<WorkDay>
{
    public void Configure(EntityTypeBuilder<WorkDay> builder)
    {
        builder.ToTable("WorkDays");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.DayOfWeek)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.MaxLessonsPerDay)
            .IsRequired()
            .HasDefaultValue(7);

        // CONTRACT §3: WorkDay.DayOfWeek unikal
        builder.HasIndex(x => x.DayOfWeek).IsUnique();
    }
}
