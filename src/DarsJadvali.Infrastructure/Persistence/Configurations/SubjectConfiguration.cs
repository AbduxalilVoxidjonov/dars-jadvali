using DarsJadvali.Domain.Entities;
using DarsJadvali.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DarsJadvali.Infrastructure.Persistence.Configurations;

public sealed class SubjectConfiguration : IEntityTypeConfiguration<Subject>
{
    public void Configure(EntityTypeBuilder<Subject> builder)
    {
        builder.ToTable("Subjects");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(x => x.Code)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(x => x.ColorCode)
            .IsRequired()
            .HasMaxLength(16)
            .HasDefaultValue("#455A64");

        // --- sxema v2 kengaytmalari ---------------------------------------
        builder.Property(x => x.ShortName).HasMaxLength(24);
        builder.Property(x => x.Distribution)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(SubjectDistribution.None);
        builder.Property(x => x.NeedsHomework).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.RequiresSpecialClassroom).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.ExternalId).HasMaxLength(64);
        builder.Property(x => x.IsDeleted).IsRequired().HasDefaultValue(false);

        builder.HasOne(x => x.AcademicYear)
            .WithMany()
            .HasForeignKey(x => x.AcademicYearId)
            .OnDelete(DeleteBehavior.Restrict);

        // CONTRACT §3: Subject.Code unikal (eski kod shunga tayanadi — o'zgarmaydi)
        builder.HasIndex(x => x.Code)
            .IsUnique()
            .HasDatabaseName("UX_Subjects_Code");

        // v2: yil ichida qisqartma noyob (faqat yilga bog'langan yozuvlar orasida).
        builder.HasIndex(x => new { x.AcademicYearId, x.ShortName })
            .IsUnique()
            .HasFilter("\"AcademicYearId\" IS NOT NULL AND \"ShortName\" IS NOT NULL AND \"IsDeleted\" = 0")
            .HasDatabaseName("UX_Subjects_AcademicYearId_ShortName");
    }
}
