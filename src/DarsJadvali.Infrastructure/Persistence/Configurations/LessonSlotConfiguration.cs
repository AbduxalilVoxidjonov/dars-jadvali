using DarsJadvali.Domain.Entities;
using DarsJadvali.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DarsJadvali.Infrastructure.Persistence.Configurations;

public sealed class LessonSlotConfiguration : IEntityTypeConfiguration<LessonSlot>
{
    public void Configure(EntityTypeBuilder<LessonSlot> builder)
    {
        builder.ToTable("LessonSlots");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.LessonNumber).IsRequired();

        builder.Property(x => x.StartTime)
            .IsRequired()
            .HasConversion(new TimeSpanToTicksConverter());

        builder.Property(x => x.EndTime)
            .IsRequired()
            .HasConversion(new TimeSpanToTicksConverter());

        // CONTRACT §3: LessonSlot.LessonNumber unikal
        builder.HasIndex(x => x.LessonNumber).IsUnique();
    }
}
