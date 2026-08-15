using DarsJadvali.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DarsJadvali.Infrastructure.Persistence.Configurations;

public sealed class StudentGroupConfiguration : IEntityTypeConfiguration<StudentGroup>
{
    public void Configure(EntityTypeBuilder<StudentGroup> builder)
    {
        builder.ToTable("StudentGroups", t =>
            t.HasCheckConstraint("CK_StudentGroups_StudentCount",
                "\"StudentCount\" IS NULL OR \"StudentCount\" >= 0"));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(64);
        builder.Property(x => x.IsEntireClass).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.ExternalId).HasMaxLength(64);
        builder.Property(x => x.IsDeleted).IsRequired().HasDefaultValue(false);

        // Egalik zanjiri: sinf yoki bo'linish o'chsa guruhlar ham o'chadi.
        // (Guruhga bog'langan dars bo'lsa LessonGroup dagi Restrict o'chirishni to'xtatadi.)
        builder.HasOne(x => x.ClassDivision)
            .WithMany(d => d.StudentGroups)
            .HasForeignKey(x => x.ClassDivisionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.SchoolClass)
            .WithMany(c => c.StudentGroups)
            .HasForeignKey(x => x.SchoolClassId)
            .OnDelete(DeleteBehavior.Cascade);

        // Har sinfda AYNAN BITTA "Butun sinf" guruhi (00 §2.7 qoida 5).
        // Nomlangan overload ishlatiladi: bir xil ustunlar ustida ikkinchi indeks
        // e'lon qilinsa EF birinchisini almashtirib yuborardi.
        builder.HasIndex(x => x.SchoolClassId, "UX_StudentGroups_SchoolClassId_EntireClass")
            .IsUnique()
            .HasFilter("\"IsEntireClass\" = 1 AND \"IsDeleted\" = 0");

        // Bir bo'linish ichida bir xil nomli ikki guruh bo'lmaydi.
        builder.HasIndex(x => new { x.ClassDivisionId, x.Name })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = 0")
            .HasDatabaseName("UX_StudentGroups_ClassDivisionId_Name");

        builder.HasIndex(x => x.SchoolClassId, "IX_StudentGroups_SchoolClassId");
    }
}
