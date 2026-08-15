using DarsJadvali.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DarsJadvali.Infrastructure.Persistence.Configurations;

public sealed class AcademicYearConfiguration : IEntityTypeConfiguration<AcademicYear>
{
    public void Configure(EntityTypeBuilder<AcademicYear> builder)
    {
        builder.ToTable("AcademicYears");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.StartYear).IsRequired();

        builder.Property(x => x.Note).HasMaxLength(500);

        // --- sxema v2 kengaytmalari ---------------------------------------
        // Hammasi DEFAULT bilan: mavjud qatorlar migratsiyada avtomatik to'ladi.
        builder.Property(x => x.DaysPerWeek).IsRequired().HasDefaultValue(6);
        builder.Property(x => x.WeeksInCycle).IsRequired().HasDefaultValue(1);
        builder.Property(x => x.TermsCount).IsRequired().HasDefaultValue(4);

        // O'quv yili nomi takrorlanmaydi.
        builder.HasIndex(x => x.Name)
            .IsUnique()
            .HasDatabaseName("UX_AcademicYears_Name");
    }
}
