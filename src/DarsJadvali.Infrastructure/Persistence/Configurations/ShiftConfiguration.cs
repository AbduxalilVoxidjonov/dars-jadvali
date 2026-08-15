using DarsJadvali.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DarsJadvali.Infrastructure.Persistence.Configurations;

public sealed class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> builder)
    {
        builder.ToTable("Shifts", t =>
            t.HasCheckConstraint("CK_Shifts_ShiftNo", "\"ShiftNo\" >= 1 AND \"ShiftNo\" <= 4"));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ShiftNo).IsRequired();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(50);
        builder.Property(x => x.ShortName).IsRequired().HasMaxLength(10);

        builder.HasOne(x => x.AcademicYear)
            .WithMany(y => y.Shifts)
            .HasForeignKey(x => x.AcademicYearId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.AcademicYearId, x.ShiftNo })
            .IsUnique()
            .HasDatabaseName("UX_Shifts_AcademicYearId_ShiftNo");
    }
}
