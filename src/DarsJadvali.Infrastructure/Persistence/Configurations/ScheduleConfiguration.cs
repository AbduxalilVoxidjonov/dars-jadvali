using DarsJadvali.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DarsJadvali.Infrastructure.Persistence.Configurations;

public sealed class ScheduleConfiguration : IEntityTypeConfiguration<Schedule>
{
    public void Configure(EntityTypeBuilder<Schedule> builder)
    {
        builder.ToTable("Schedules");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.CreatedAt).IsRequired();

        // --- sxema v2 kengaytmalari ---------------------------------------
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.Property(x => x.WeeksInCycle).IsRequired().HasDefaultValue(1);

        // O'quv yili o'chirilsa uning barcha jadvallari ham o'chadi.
        builder.HasOne(x => x.AcademicYear)
            .WithMany(y => y.Schedules)
            .HasForeignKey(x => x.AcademicYearId)
            .OnDelete(DeleteBehavior.Cascade);

        // Chorak — ma'lumotnoma: chorak o'chsa jadval jimgina yo'qolmasin.
        builder.HasOne(x => x.Term)
            .WithMany(t => t.Schedules)
            .HasForeignKey(x => x.TermId)
            .OnDelete(DeleteBehavior.Restrict);

        // Nusxa olingan manba jadval (o'ziga havola) — manba o'chsa nusxa qolaveradi.
        builder.HasOne(x => x.CopiedFromSchedule)
            .WithMany()
            .HasForeignKey(x => x.CopiedFromScheduleId)
            .OnDelete(DeleteBehavior.Restrict);

        // Bitta o'quv yili ichida jadval nomi takrorlanmaydi.
        builder.HasIndex(x => new { x.AcademicYearId, x.Name })
            .IsUnique()
            .HasDatabaseName("UX_Schedules_AcademicYearId_Name");

        // Faol jadval — 00 §5.1 talab qilgan FILTRLANGAN UNIKAL indeks: ayni paytda
        // faqat bitta faol jadval bo'lishi mumkin.
        // Bu endi mumkin, chunki ScheduleSetService.SetActiveAsync tranzaksiyaga ko'chdi
        // va "avval hammasini o'chir → keyin bittasini yoq" tartibida ishlaydi
        // (oraliq holatda 0 ta faol jadval bo'ladi, indeks buzilmaydi).
        builder.HasIndex(x => x.IsActive)
            .IsUnique()
            .HasFilter("\"IsActive\" = 1")
            .HasDatabaseName("UX_Schedules_IsActive");

        builder.HasIndex(x => x.TermId)
            .HasDatabaseName("IX_Schedules_TermId");

        builder.Navigation(x => x.AcademicYear).AutoInclude();
    }
}
