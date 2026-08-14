using DarsJadvali.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DarsJadvali.Infrastructure.Persistence.Configurations;

public sealed class TeacherAssignmentConfiguration : IEntityTypeConfiguration<TeacherAssignment>
{
    public void Configure(EntityTypeBuilder<TeacherAssignment> builder)
    {
        builder.ToTable("TeacherAssignments");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.WeeklyHoursCount).IsRequired();

        builder.HasOne(x => x.Teacher)
            .WithMany(t => t.Assignments)
            .HasForeignKey(x => x.TeacherId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Subject)
            .WithMany(s => s.Assignments)
            .HasForeignKey(x => x.SubjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ClassGroup)
            .WithMany(c => c.Assignments)
            .HasForeignKey(x => x.ClassGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        // CONTRACT §3: unikal (TeacherId, SubjectId, ClassGroupId)
        builder.HasIndex(x => new { x.TeacherId, x.SubjectId, x.ClassGroupId }).IsUnique();

        // Application qatlami EF Core'ni ko'rmaydi — navigatsiyalar avtomatik yuklanadi.
        builder.Navigation(x => x.Teacher).AutoInclude();
        builder.Navigation(x => x.Subject).AutoInclude();
        builder.Navigation(x => x.ClassGroup).AutoInclude();
    }
}
