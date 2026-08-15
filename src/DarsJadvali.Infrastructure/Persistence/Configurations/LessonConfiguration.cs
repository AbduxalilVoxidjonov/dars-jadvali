using DarsJadvali.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DarsJadvali.Infrastructure.Persistence.Configurations;

public sealed class LessonConfiguration : IEntityTypeConfiguration<Lesson>
{
    public void Configure(EntityTypeBuilder<Lesson> builder)
    {
        builder.ToTable("Lessons", t =>
        {
            // Ma'nosiz dars ta'rifi bo'lmasin (00 §5.2).
            t.HasCheckConstraint("CK_Lessons_PeriodsPerWeek", "\"PeriodsPerWeek\" > 0");
            t.HasCheckConstraint("CK_Lessons_PeriodsPerCard",
                "\"PeriodsPerCard\" >= 1 AND \"PeriodsPerCard\" <= 8");
            t.HasCheckConstraint("CK_Lessons_PeriodsConsistency",
                "\"PeriodsPerWeek\" >= \"PeriodsPerCard\"");
            t.HasCheckConstraint("CK_Lessons_RequiredClassroomCount",
                "\"RequiredClassroomCount\" >= 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.PeriodsPerWeek).IsRequired();
        // HasDefaultValue(1) ishlatilmaydi — CardConfiguration dagi izohga qarang
        // (CLR sentinel tufayli CHECK cheklovi chetlab o'tilardi).
        builder.Property(x => x.PeriodsPerCard).IsRequired();
        builder.Property(x => x.AllowedDaysMask).IsRequired().HasDefaultValue(0);
        builder.Property(x => x.AllowedWeeksMask).IsRequired().HasDefaultValue(0);
        builder.Property(x => x.Priority).IsRequired().HasDefaultValue(0);
        builder.Property(x => x.RequiredClassroomCount).IsRequired().HasDefaultValue(0);
        builder.Property(x => x.ExternalId).HasMaxLength(64);

        builder.HasOne(x => x.AcademicYear)
            .WithMany()
            .HasForeignKey(x => x.AcademicYearId)
            .OnDelete(DeleteBehavior.Cascade);

        // Fan — ma'lumotnoma: bog'liq dars borligida fan JIMGINA o'chib ketmasin.
        builder.HasOne(x => x.Subject)
            .WithMany()
            .HasForeignKey(x => x.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);

        // Backfill izi — takror ishga tushirilsa dublikat yaratmaydi.
        builder.HasIndex(x => x.LegacyTeacherAssignmentId)
            .IsUnique()
            .HasFilter("\"LegacyTeacherAssignmentId\" IS NOT NULL")
            .HasDatabaseName("UX_Lessons_LegacyTeacherAssignmentId");

        builder.HasIndex(x => new { x.AcademicYearId, x.SubjectId })
            .HasDatabaseName("IX_Lessons_AcademicYearId_SubjectId");

        // DIQQAT: (Teacher, Subject, Class) uchligi bo'yicha unikal indeks ATAYLAB YO'Q.
        // aSc'da bitta uchlik uchun bir nechta Lesson normal (1×juft + 1×yakka dars).
    }
}
