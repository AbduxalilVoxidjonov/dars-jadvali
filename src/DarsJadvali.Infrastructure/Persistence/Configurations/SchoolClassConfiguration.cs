using DarsJadvali.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DarsJadvali.Infrastructure.Persistence.Configurations;

public sealed class SchoolClassConfiguration : IEntityTypeConfiguration<SchoolClass>
{
    public void Configure(EntityTypeBuilder<SchoolClass> builder)
    {
        builder.ToTable("SchoolClasses", t =>
            t.HasCheckConstraint("CK_SchoolClasses_StudentCount", "\"StudentCount\" >= 0"));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(50);
        builder.Property(x => x.ShortName).IsRequired().HasMaxLength(24);
        builder.Property(x => x.Language).HasMaxLength(32);
        builder.Property(x => x.StudentCount).IsRequired().HasDefaultValue(0);
        builder.Property(x => x.ExternalId).HasMaxLength(64);
        builder.Property(x => x.IsDeleted).IsRequired().HasDefaultValue(false);

        builder.HasOne(x => x.AcademicYear)
            .WithMany()
            .HasForeignKey(x => x.AcademicYearId)
            .OnDelete(DeleteBehavior.Cascade);

        // Ma'lumotnomalarga havolalar — Restrict: bog'liq sinf borligida
        // parallel/smena/o'qituvchi/xona JIMGINA o'chib ketmasin.
        builder.HasOne(x => x.Grade)
            .WithMany(g => g.SchoolClasses)
            .HasForeignKey(x => x.GradeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Shift)
            .WithMany(s => s.SchoolClasses)
            .HasForeignKey(x => x.ShiftId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ClassTeacher)
            .WithMany()
            .HasForeignKey(x => x.ClassTeacherId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.HomeClassroom)
            .WithMany()
            .HasForeignKey(x => x.HomeClassroomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.AcademicYearId, x.Name })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = 0")
            .HasDatabaseName("UX_SchoolClasses_AcademicYearId_Name");

        builder.HasIndex(x => new { x.AcademicYearId, x.ShortName })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = 0")
            .HasDatabaseName("UX_SchoolClasses_AcademicYearId_ShortName");

        // Backfill izi: bitta eski ClassGroup faqat bitta SchoolClass'ga ko'chadi —
        // shu tufayli backfill takror ishga tushsa ham dublikat yaratmaydi.
        builder.HasIndex(x => x.LegacyClassGroupId)
            .IsUnique()
            .HasFilter("\"LegacyClassGroupId\" IS NOT NULL")
            .HasDatabaseName("UX_SchoolClasses_LegacyClassGroupId");

        builder.HasIndex(x => x.ShiftId).HasDatabaseName("IX_SchoolClasses_ShiftId");
    }
}
