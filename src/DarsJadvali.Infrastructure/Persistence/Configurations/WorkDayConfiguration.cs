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

        // --- sxema v2 kengaytmalari ---------------------------------------
        builder.Property(x => x.DayNo).IsRequired().HasDefaultValue(0);
        builder.Property(x => x.Name).HasMaxLength(20);
        builder.Property(x => x.ShortName).HasMaxLength(5);
        builder.Property(x => x.MinLessonsPerDay).IsRequired().HasDefaultValue(0);

        builder.HasOne(x => x.AcademicYear)
            .WithMany()
            .HasForeignKey(x => x.AcademicYearId)
            .OnDelete(DeleteBehavior.Cascade);

        // CONTRACT §3: WorkDay.DayOfWeek unikal (eski, yilga bog'lanmagan yozuvlar uchun).
        builder.HasIndex(x => x.DayOfWeek)
            .IsUnique()
            .HasDatabaseName("UX_WorkDays_DayOfWeek");

        // Yilga bog'langan yozuvlar uchun (AcademicYearId, DayNo) unikal.
        // Filtr: eski global yozuvlar (AcademicYearId IS NULL) bu indeksga tushmaydi.
        builder.HasIndex(x => new { x.AcademicYearId, x.DayNo })
            .IsUnique()
            .HasFilter("\"AcademicYearId\" IS NOT NULL")
            .HasDatabaseName("UX_WorkDays_AcademicYearId_DayNo");
    }
}
