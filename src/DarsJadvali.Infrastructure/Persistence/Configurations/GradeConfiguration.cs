using DarsJadvali.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DarsJadvali.Infrastructure.Persistence.Configurations;

public sealed class GradeConfiguration : IEntityTypeConfiguration<Grade>
{
    public void Configure(EntityTypeBuilder<Grade> builder)
    {
        builder.ToTable("Grades", t =>
            t.HasCheckConstraint("CK_Grades_GradeNo", "\"GradeNo\" >= 0 AND \"GradeNo\" <= 20"));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.GradeNo).IsRequired();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(50);
        builder.Property(x => x.ShortName).IsRequired().HasMaxLength(16);
        builder.Property(x => x.IsDeleted).IsRequired().HasDefaultValue(false);

        builder.HasOne(x => x.AcademicYear)
            .WithMany()
            .HasForeignKey(x => x.AcademicYearId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.AcademicYearId, x.GradeNo })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = 0")
            .HasDatabaseName("UX_Grades_AcademicYearId_GradeNo");
    }
}
