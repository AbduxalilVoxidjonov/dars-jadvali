using DarsJadvali.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DarsJadvali.Infrastructure.Persistence.Configurations;

/// <summary>
/// Sxemaning eng muhim konfiguratsiyasi: bandlik DB darajasida shu yerda kafolatlanadi.
/// </summary>
public sealed class CardOccurrenceConfiguration : IEntityTypeConfiguration<CardOccurrence>
{
    public void Configure(EntityTypeBuilder<CardOccurrence> builder)
    {
        builder.ToTable("CardOccurrences", t =>
        {
            t.HasCheckConstraint("CK_CardOccurrences_DayNo", "\"DayNo\" >= 0 AND \"DayNo\" <= 13");
            t.HasCheckConstraint("CK_CardOccurrences_PeriodNo", "\"PeriodNo\" >= 0");
            t.HasCheckConstraint("CK_CardOccurrences_WeekNo", "\"WeekNo\" >= 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ResourceKind).IsRequired().HasConversion<int>();
        builder.Property(x => x.ResourceId).IsRequired();

        builder.HasOne(x => x.Schedule)
            .WithMany()
            .HasForeignKey(x => x.ScheduleId)
            .OnDelete(DeleteBehavior.Cascade);

        // Hosila jadval: kartochka o'chsa bandlik qatorlari ham o'chadi.
        builder.HasOne(x => x.Card)
            .WithMany(c => c.Occurrences)
            .HasForeignKey(x => x.CardId)
            .OnDelete(DeleteBehavior.Cascade);

        // ================================================================
        // SXEMANING ENG MUHIM INDEKSI
        // ----------------------------------------------------------------
        // Bitta indeks uchta kafolatni beradi:
        //   * o'qituvchi bir slotda ikki joyda bo'lolmaydi (smenalar bo'ylab ham,
        //     chunki PeriodNo smenalar bo'ylab uzluksiz);
        //   * guruh bir slotda ikki darsda bo'lolmaydi;
        //   * xona bir slotda ikki marta band bo'lolmaydi.
        //
        // TermNo ustuni ATAYLAB YO'Q: chorak = alohida Schedule varianti (tasdiqlangan
        // qaror), ya'ni chorak allaqachon ScheduleId ichida.
        //
        // Nom BARQAROR bo'lishi shart (00 §5.4): Desktop ViewModel'lari SQLite xato
        // matnini tahlil qiladi, indeks nomi o'zgarsa jimgina buziladi.
        // ================================================================
        builder.HasIndex(x => new
            {
                x.ScheduleId,
                x.ResourceKind,
                x.ResourceId,
                x.DayNo,
                x.PeriodNo,
                x.WeekNo
            })
            .IsUnique()
            .HasDatabaseName("UX_CardOccurrences_Schedule_Resource_Slot");

        // Jadval ekranini chizish uchun.
        builder.HasIndex(x => new { x.ScheduleId, x.DayNo, x.PeriodNo })
            .HasDatabaseName("IX_CardOccurrences_Schedule_Day_Period");

        builder.HasIndex(x => x.CardId).HasDatabaseName("IX_CardOccurrences_CardId");
    }
}
