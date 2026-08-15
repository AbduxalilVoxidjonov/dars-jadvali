using DarsJadvali.Domain.Entities;
using DarsJadvali.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DarsJadvali.Infrastructure.Persistence.Configurations;

public sealed class PeriodConfiguration : IEntityTypeConfiguration<Period>
{
    public void Configure(EntityTypeBuilder<Period> builder)
    {
        builder.ToTable("Periods", t =>
        {
            t.HasCheckConstraint("CK_Periods_PeriodNo", "\"PeriodNo\" >= 0");
            t.HasCheckConstraint("CK_Periods_TimeOrder", "\"EndTime\" > \"StartTime\"");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.PeriodNo).IsRequired();

        // Yarim tundan daqiqa (int) — ticks emas: baza faylini qo'lda o'qish mumkin.
        builder.Property(x => x.StartTime)
            .IsRequired()
            .HasConversion(new TimeOnlyToMinutesConverter());

        builder.Property(x => x.EndTime)
            .IsRequired()
            .HasConversion(new TimeOnlyToMinutesConverter());

        builder.Property(x => x.Name).HasMaxLength(50);
        builder.Property(x => x.ShortName).HasMaxLength(10);
        builder.Property(x => x.IsBreak).IsRequired().HasDefaultValue(false);

        builder.HasOne(x => x.AcademicYear)
            .WithMany(y => y.Periods)
            .HasForeignKey(x => x.AcademicYearId)
            .OnDelete(DeleteBehavior.Cascade);

        // Smena — ma'lumotnoma: o'chirilsa dars soatlari jimgina yo'qolmasin.
        builder.HasOne(x => x.Shift)
            .WithMany(s => s.Periods)
            .HasForeignKey(x => x.ShiftId)
            .OnDelete(DeleteBehavior.Restrict);

        // PeriodNo o'quv yili ichida GLOBAL: 1-smena 1..6, 2-smena 7..12.
        // Aynan shu tufayli CardOccurrence ning yagona indeksi smenalararo
        // o'qituvchi to'qnashuvini ham ushlaydi.
        builder.HasIndex(x => new { x.AcademicYearId, x.PeriodNo })
            .IsUnique()
            .HasDatabaseName("UX_Periods_AcademicYearId_PeriodNo");

        builder.HasIndex(x => x.ShiftId)
            .HasDatabaseName("IX_Periods_ShiftId");
    }
}
