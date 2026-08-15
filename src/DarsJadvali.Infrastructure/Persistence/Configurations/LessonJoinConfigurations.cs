using DarsJadvali.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DarsJadvali.Infrastructure.Persistence.Configurations;

/// <summary>
/// Dars ↔ o'qituvchi. Kompozit PK takroriy bog'lanishni imkonsiz qiladi.
/// </summary>
public sealed class LessonTeacherConfiguration : IEntityTypeConfiguration<LessonTeacher>
{
    public void Configure(EntityTypeBuilder<LessonTeacher> builder)
    {
        builder.ToTable("LessonTeachers");
        builder.HasKey(x => new { x.LessonId, x.TeacherId });

        // Egalik zanjiri: dars o'chsa bog'lanishlari ham o'chadi.
        builder.HasOne(x => x.Lesson)
            .WithMany(l => l.Teachers)
            .HasForeignKey(x => x.LessonId)
            .OnDelete(DeleteBehavior.Cascade);

        // O'qituvchi — ma'lumotnoma: bog'liq darsi bor o'qituvchi o'chirilmaydi.
        // Bu ATAYLAB Restrict: eski modelda Cascade edi va butun jadval jimgina yo'q bo'lardi.
        builder.HasOne(x => x.Teacher)
            .WithMany()
            .HasForeignKey(x => x.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.TeacherId).HasDatabaseName("IX_LessonTeachers_TeacherId");
    }
}

/// <summary>Dars ↔ sinf.</summary>
public sealed class LessonClassConfiguration : IEntityTypeConfiguration<LessonClass>
{
    public void Configure(EntityTypeBuilder<LessonClass> builder)
    {
        builder.ToTable("LessonClasses");
        builder.HasKey(x => new { x.LessonId, x.SchoolClassId });

        builder.HasOne(x => x.Lesson)
            .WithMany(l => l.Classes)
            .HasForeignKey(x => x.LessonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.SchoolClass)
            .WithMany()
            .HasForeignKey(x => x.SchoolClassId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.SchoolClassId).HasDatabaseName("IX_LessonClasses_SchoolClassId");
    }
}

/// <summary>Dars ↔ guruh. Bandlik aynan shu bog'lanishdan hosil bo'ladi.</summary>
public sealed class LessonGroupConfiguration : IEntityTypeConfiguration<LessonGroup>
{
    public void Configure(EntityTypeBuilder<LessonGroup> builder)
    {
        builder.ToTable("LessonGroups");
        builder.HasKey(x => new { x.LessonId, x.StudentGroupId });

        builder.HasOne(x => x.Lesson)
            .WithMany(l => l.Groups)
            .HasForeignKey(x => x.LessonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.StudentGroup)
            .WithMany()
            .HasForeignKey(x => x.StudentGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.StudentGroupId).HasDatabaseName("IX_LessonGroups_StudentGroupId");
    }
}

/// <summary>Dars ↔ ruxsat etilgan xonalar (P1).</summary>
public sealed class LessonClassroomConfiguration : IEntityTypeConfiguration<LessonClassroom>
{
    public void Configure(EntityTypeBuilder<LessonClassroom> builder)
    {
        builder.ToTable("LessonClassrooms");
        builder.HasKey(x => new { x.LessonId, x.ClassroomId });

        builder.Property(x => x.Priority).IsRequired().HasDefaultValue(0);

        builder.HasOne(x => x.Lesson)
            .WithMany(l => l.Classrooms)
            .HasForeignKey(x => x.LessonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Classroom)
            .WithMany()
            .HasForeignKey(x => x.ClassroomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ClassroomId).HasDatabaseName("IX_LessonClassrooms_ClassroomId");
    }
}

/// <summary>Kartochka ↔ tayinlangan xona (P1).</summary>
public sealed class CardClassroomConfiguration : IEntityTypeConfiguration<CardClassroom>
{
    public void Configure(EntityTypeBuilder<CardClassroom> builder)
    {
        builder.ToTable("CardClassrooms");
        builder.HasKey(x => new { x.CardId, x.ClassroomId });

        builder.HasOne(x => x.Card)
            .WithMany(c => c.Classrooms)
            .HasForeignKey(x => x.CardId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Classroom)
            .WithMany()
            .HasForeignKey(x => x.ClassroomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ClassroomId).HasDatabaseName("IX_CardClassrooms_ClassroomId");
    }
}
