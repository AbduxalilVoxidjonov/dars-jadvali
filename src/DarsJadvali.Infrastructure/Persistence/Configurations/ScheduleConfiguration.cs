using DarsJadvali.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DarsJadvali.Infrastructure.Persistence.Configurations;

public sealed class ScheduleConfiguration : IEntityTypeConfiguration<Schedule>
{
    public void Configure(EntityTypeBuilder<Schedule> builder)
    {
        builder.ToTable("Schedules");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.CreatedAt).IsRequired();

        // O'quv yili o'chirilsa uning barcha jadvallari ham o'chadi.
        builder.HasOne(x => x.AcademicYear)
            .WithMany(y => y.Schedules)
            .HasForeignKey(x => x.AcademicYearId)
            .OnDelete(DeleteBehavior.Cascade);

        // Bitta o'quv yili ichida jadval nomi takrorlanmaydi.
        builder.HasIndex(x => new { x.AcademicYearId, x.Name }).IsUnique();

        // Faol jadvalni tez topish uchun.
        builder.HasIndex(x => x.IsActive);

        builder.Navigation(x => x.AcademicYear).AutoInclude();
    }
}
