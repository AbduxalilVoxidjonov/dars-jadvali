using DarsJadvali.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DarsJadvali.Infrastructure.Persistence.Configurations;

public sealed class ClassGroupConfiguration : IEntityTypeConfiguration<ClassGroup>
{
    public void Configure(EntityTypeBuilder<ClassGroup> builder)
    {
        builder.ToTable("ClassGroups");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.RoomNumber)
            .HasMaxLength(50);

        builder.Property(x => x.StudentCount)
            .IsRequired();

        // CONTRACT §3: ClassGroup.Name unikal
        builder.HasIndex(x => x.Name).IsUnique();
    }
}
