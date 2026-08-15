using DarsJadvali.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DarsJadvali.Infrastructure.Persistence.Configurations;

public sealed class TimeOffConfiguration : IEntityTypeConfiguration<TimeOff>
{
    public void Configure(EntityTypeBuilder<TimeOff> builder)
    {
        builder.ToTable("TimeOffs", t =>
        {
            t.HasCheckConstraint("CK_TimeOffs_DayNo", "\"DayNo\" >= 0 AND \"DayNo\" <= 13");
            t.HasCheckConstraint("CK_TimeOffs_PeriodNo", "\"PeriodNo\" >= 0");
            t.HasCheckConstraint("CK_TimeOffs_Penalty", "\"Penalty\" >= 0 AND \"Penalty\" <= 1000");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OwnerKind).IsRequired().HasConversion<int>();
        builder.Property(x => x.OwnerId).IsRequired();
        builder.Property(x => x.DayNo).IsRequired();
        builder.Property(x => x.PeriodNo).IsRequired();
        // HasDefaultValue(0) bu YERDA xavfsiz va ataylab qoldirilgan: standart qiymat CLR
        // standarti bilan BIR XIL (0) va 0 hech qanday CHECK'ni buzmaydi
        // (WeeksMask = 0 → "barcha haftalar", Penalty = 0 → jarimasiz). Tuzoq ustun
        // standarti CLR standartidan FARQ qilganda paydo bo'ladi — o'sha holatda EF
        // ustunni INSERT'dan tushirib qoldiradi va CHECK ishlamay qoladi (00 §10.4).
        builder.Property(x => x.WeeksMask).IsRequired().HasDefaultValue(0);
        builder.Property(x => x.Availability).IsRequired().HasConversion<int>();
        builder.Property(x => x.Penalty).IsRequired().HasDefaultValue(0);

        // V2_06 ko'chirish izi — unikal EMAS: bitta eski vaqt oralig'i bir nechta
        // (kun, soat) katakchasiga yoyiladi.
        builder.HasIndex(x => x.LegacyTeacherAvailabilityId)
            .HasFilter("\"LegacyTeacherAvailabilityId\" IS NOT NULL")
            .HasDatabaseName("IX_TimeOffs_LegacyTeacherAvailabilityId");

        builder.HasOne(x => x.AcademicYear)
            .WithMany()
            .HasForeignKey(x => x.AcademicYearId)
            .OnDelete(DeleteBehavior.Cascade);

        // Ustma-ust tushuvchi cheklovlar imkonsiz: bitta (ega, kun, soat, haftalar)
        // katakchasi uchun aynan bitta qator.
        builder.HasIndex(x => new
            {
                x.AcademicYearId,
                x.OwnerKind,
                x.OwnerId,
                x.DayNo,
                x.PeriodNo,
                x.WeeksMask
            })
            .IsUnique()
            .HasDatabaseName("UX_TimeOffs_Owner_Slot");
    }
}
