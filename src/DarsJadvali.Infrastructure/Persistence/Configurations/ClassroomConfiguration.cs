using DarsJadvali.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DarsJadvali.Infrastructure.Persistence.Configurations;

/// <summary>
/// Xona moduli P1: jadval va FK'lar tayyor, lekin hech qayerda majburiy emas.
/// Xona ro'yxati bo'sh bo'lsa ham butun tizim ishlaydi.
/// </summary>
public sealed class ClassroomConfiguration : IEntityTypeConfiguration<Classroom>
{
    public void Configure(EntityTypeBuilder<Classroom> builder)
    {
        builder.ToTable("Classrooms", t =>
            t.HasCheckConstraint("CK_Classrooms_Capacity",
                "\"Capacity\" IS NULL OR \"Capacity\" > 0"));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(128);
        builder.Property(x => x.ShortName).IsRequired().HasMaxLength(24);
        builder.Property(x => x.Kind).IsRequired().HasConversion<int>();
        builder.Property(x => x.IsShared).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.ExternalId).HasMaxLength(64);
        builder.Property(x => x.IsDeleted).IsRequired().HasDefaultValue(false);

        // V2_07 ko'chirish izi: eski erkin matnli xona nomi.
        builder.Property(x => x.LegacySourceName).HasMaxLength(50);

        builder.HasOne(x => x.AcademicYear)
            .WithMany()
            .HasForeignKey(x => x.AcademicYearId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.AcademicYearId, x.ShortName })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = 0")
            .HasDatabaseName("UX_Classrooms_AcademicYearId_ShortName");

        // Ko'chirish idempotentligining KAFOLATI: bir xil eski matndan ikkinchi xona
        // yaratilmaydi. "IsDeleted" filtrda YO'Q — o'chirilgan xona ham qayta yaratilmasin.
        builder.HasIndex(x => new { x.AcademicYearId, x.LegacySourceName })
            .IsUnique()
            .HasFilter("\"LegacySourceName\" IS NOT NULL")
            .HasDatabaseName("UX_Classrooms_AcademicYearId_LegacySourceName");
    }
}
