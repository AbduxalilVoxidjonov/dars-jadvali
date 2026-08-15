using DarsJadvali.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DarsJadvali.Infrastructure.Persistence.Configurations;

public sealed class ClassDivisionConfiguration : IEntityTypeConfiguration<ClassDivision>
{
    public void Configure(EntityTypeBuilder<ClassDivision> builder)
    {
        builder.ToTable("ClassDivisions", t =>
            t.HasCheckConstraint("CK_ClassDivisions_DivisionTag", "\"DivisionTag\" >= 0"));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DivisionTag).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(64);

        // Egalik zanjiri: sinf o'chsa bo'linishlari ham o'chadi.
        builder.HasOne(x => x.SchoolClass)
            .WithMany(c => c.Divisions)
            .HasForeignKey(x => x.SchoolClassId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.SchoolClassId, x.DivisionTag })
            .IsUnique()
            .HasDatabaseName("UX_ClassDivisions_SchoolClassId_DivisionTag");
    }
}
