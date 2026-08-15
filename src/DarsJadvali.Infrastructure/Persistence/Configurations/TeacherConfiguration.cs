using DarsJadvali.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DarsJadvali.Infrastructure.Persistence.Configurations;

public sealed class TeacherConfiguration : IEntityTypeConfiguration<Teacher>
{
    public void Configure(EntityTypeBuilder<Teacher> builder)
    {
        builder.ToTable("Teachers");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.FullName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Phone)
            .HasMaxLength(50);

        builder.Property(x => x.ColorCode)
            .IsRequired()
            .HasMaxLength(16)
            .HasDefaultValue("#1976D2");

        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // --- sxema v2 kengaytmalari ---------------------------------------
        builder.Property(x => x.ShortName).HasMaxLength(24);
        builder.Property(x => x.FirstName).HasMaxLength(128);
        builder.Property(x => x.LastName).HasMaxLength(128);
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.Property(x => x.Gender).HasConversion<int?>();

        // Yuklama nazorati: shartnoma soati + stavka ulushi.
        builder.Property(x => x.ContractRate).HasColumnType("decimal(4,2)");

        builder.Property(x => x.IsVacancy).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.ExternalId).HasMaxLength(64);
        builder.Property(x => x.IsDeleted).IsRequired().HasDefaultValue(false);

        builder.HasOne(x => x.AcademicYear)
            .WithMany()
            .HasForeignKey(x => x.AcademicYearId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.FullName).HasDatabaseName("IX_Teachers_FullName");

        // Qisqartma yil ichida noyob (faqat to'ldirilgan va o'chirilmaganlar orasida).
        builder.HasIndex(x => new { x.AcademicYearId, x.ShortName })
            .IsUnique()
            .HasFilter("\"AcademicYearId\" IS NOT NULL AND \"ShortName\" IS NOT NULL AND \"IsDeleted\" = 0")
            .HasDatabaseName("UX_Teachers_AcademicYearId_ShortName");
    }
}
