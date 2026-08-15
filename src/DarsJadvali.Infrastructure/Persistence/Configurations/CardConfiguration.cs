using DarsJadvali.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DarsJadvali.Infrastructure.Persistence.Configurations;

public sealed class CardConfiguration : IEntityTypeConfiguration<Card>
{
    public void Configure(EntityTypeBuilder<Card> builder)
    {
        builder.ToTable("Cards", t =>
        {
            t.HasCheckConstraint("CK_Cards_DayNo", "\"DayNo\" >= 0 AND \"DayNo\" <= 13");
            // Joylashtirilmagan kartochka "Card" emas — u shunchaki mavjud emas.
            t.HasCheckConstraint("CK_Cards_WeeksMask", "\"WeeksMask\" > 0");
            // Uzunlik Lesson.PeriodsPerCard bilan bir xil chegarada (1..8).
            t.HasCheckConstraint("CK_Cards_Length", "\"Length\" >= 1 AND \"Length\" <= 8");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DayNo).IsRequired();
        // DIQQAT: HasDefaultValue(1) ATAYLAB ISHLATILMAYDI. EF Core CLR standart qiymatini
        // (int uchun 0) "sentinel" deb hisoblaydi va bunday ustunni INSERT'dan tushirib
        // qoldiradi — natijada WeeksMask = 0 jimgina 1 ga aylanib, CK_Cards_WeeksMask
        // cheklovi hech qachon ishlamas edi. C# tomonidagi `= 1` boshlang'ich qiymati yetarli.
        builder.Property(x => x.WeeksMask).IsRequired();

        // AYNAN SHU SABABGA KO'RA bu yerda ham HasDefaultValue(1) YO'Q: EF Core `Length = 0`
        // ni "sentinel" deb bilib ustunni INSERT'dan tushirib qoldirardi va CK_Cards_Length
        // hech qachon ishlamas edi. C# tomonidagi `= 1` boshlang'ich qiymati yetarli.
        builder.Property(x => x.Length).IsRequired();

        builder.Property(x => x.IsLocked).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.LegacyRoomNumber).HasMaxLength(50);

        // Egalik zanjiri: jadval varianti o'chsa kartochkalari ham o'chadi.
        builder.HasOne(x => x.Schedule)
            .WithMany()
            .HasForeignKey(x => x.ScheduleId)
            .OnDelete(DeleteBehavior.Cascade);

        // Dars ta'rifi o'chsa uning joylashtirishlari ham ma'nosiz qoladi.
        builder.HasOne(x => x.Lesson)
            .WithMany(l => l.Cards)
            .HasForeignKey(x => x.LessonId)
            .OnDelete(DeleteBehavior.Cascade);

        // Dars soati — ma'lumotnoma: bog'liq kartochka borligida o'chirilmaydi.
        builder.HasOne(x => x.Period)
            .WithMany()
            .HasForeignKey(x => x.PeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        // Bir dars bir slotga ikki marta qo'yilmaydi.
        builder.HasIndex(x => new { x.ScheduleId, x.LessonId, x.DayNo, x.PeriodId, x.WeeksMask })
            .IsUnique()
            .HasDatabaseName("UX_Cards_Schedule_Lesson_Day_Period_Weeks");

        builder.HasIndex(x => new { x.ScheduleId, x.DayNo })
            .HasDatabaseName("IX_Cards_ScheduleId_DayNo");

        builder.HasIndex(x => x.PeriodId).HasDatabaseName("IX_Cards_PeriodId");

        // Backfill izi — takror ishga tushirilsa dublikat yaratmaydi.
        builder.HasIndex(x => x.LegacyScheduleEntryId)
            .IsUnique()
            .HasFilter("\"LegacyScheduleEntryId\" IS NOT NULL")
            .HasDatabaseName("UX_Cards_LegacyScheduleEntryId");
    }
}
