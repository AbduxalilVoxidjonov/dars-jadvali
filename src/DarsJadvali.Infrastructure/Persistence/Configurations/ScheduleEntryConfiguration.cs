using DarsJadvali.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DarsJadvali.Infrastructure.Persistence.Configurations;

public sealed class ScheduleEntryConfiguration : IEntityTypeConfiguration<ScheduleEntry>
{
    public void Configure(EntityTypeBuilder<ScheduleEntry> builder)
    {
        builder.ToTable("ScheduleEntries");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.DayOfWeek)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.LessonNumber).IsRequired();

        builder.Property(x => x.RoomNumber).HasMaxLength(50);

        // Jadval o'chirilsa uning barcha dars yozuvlari ham o'chadi.
        builder.HasOne(x => x.Schedule)
            .WithMany(s => s.Entries)
            .HasForeignKey(x => x.ScheduleId)
            .OnDelete(DeleteBehavior.Cascade);

        // Ma'lumotnomalar — RESTRICT (00 §10.8, 4-band).
        // Ilgari bular Cascade edi: bitta o'qituvchini o'chirish uning BUTUN jadvalini
        // jimgina o'chirib yuborardi va foydalanuvchi buni bilmasdi. Endi baza rad etadi,
        // Application esa tipli xato beradi (SqliteExceptionTranslator).
        // Sxema v2 dagi LessonTeacher/LessonClass/LessonGroup allaqachon shunday ishlaydi.
        builder.HasOne(x => x.ClassGroup)
            .WithMany(c => c.ScheduleEntries)
            .HasForeignKey(x => x.ClassGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Subject)
            .WithMany(s => s.ScheduleEntries)
            .HasForeignKey(x => x.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Teacher)
            .WithMany(t => t.ScheduleEntries)
            .HasForeignKey(x => x.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);

        // CONTRACT §3: ikkita unikal indeks — endi HAR BIR JADVAL ichida alohida amal qiladi,
        // shunda turli o'quv yili/variantda bir xil o'rin band bo'lishi mumkin.
        builder.HasIndex(x => new { x.ScheduleId, x.ClassGroupId, x.DayOfWeek, x.LessonNumber }).IsUnique();
        builder.HasIndex(x => new { x.ScheduleId, x.TeacherId, x.DayOfWeek, x.LessonNumber }).IsUnique();

        builder.Navigation(x => x.Teacher).AutoInclude();
        builder.Navigation(x => x.Subject).AutoInclude();
        builder.Navigation(x => x.ClassGroup).AutoInclude();
    }
}
