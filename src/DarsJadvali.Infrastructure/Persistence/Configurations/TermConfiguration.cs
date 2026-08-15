using DarsJadvali.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DarsJadvali.Infrastructure.Persistence.Configurations;

public sealed class TermConfiguration : IEntityTypeConfiguration<Term>
{
    public void Configure(EntityTypeBuilder<Term> builder)
    {
        builder.ToTable("Terms", t =>
            t.HasCheckConstraint("CK_Terms_Ordinal", "\"Ordinal\" >= 1 AND \"Ordinal\" <= 12"));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Ordinal).IsRequired();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(50);
        builder.Property(x => x.ShortName).IsRequired().HasMaxLength(10);

        // Egalik zanjiri: o'quv yili o'chsa choraklari ham o'chadi.
        builder.HasOne(x => x.AcademicYear)
            .WithMany(y => y.Terms)
            .HasForeignKey(x => x.AcademicYearId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.AcademicYearId, x.Ordinal })
            .IsUnique()
            .HasDatabaseName("UX_Terms_AcademicYearId_Ordinal");
    }
}
