using System.Linq.Expressions;
using DarsJadvali.Domain.Common;
using DarsJadvali.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DarsJadvali.Infrastructure.Persistence;

/// <summary>Dastur ma'lumotlar bazasi konteksti (SQLite).</summary>
public partial class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // -------------------------------------------------------------------------
    // Eski (v1) model — Application/Desktop/Web hozircha shularni ishlatadi.
    // 1-bosqich ADDITIV: bu jadvallar o'chirilmaydi va nomi o'zgartirilmaydi.
    // -------------------------------------------------------------------------

    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<ClassGroup> ClassGroups => Set<ClassGroup>();
    public DbSet<TeacherAssignment> TeacherAssignments => Set<TeacherAssignment>();
    public DbSet<WorkDay> WorkDays => Set<WorkDay>();
    public DbSet<TeacherAvailability> TeacherAvailabilities => Set<TeacherAvailability>();
    public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();
    public DbSet<Schedule> Schedules => Set<Schedule>();
    public DbSet<ScheduleEntry> ScheduleEntries => Set<ScheduleEntry>();
    public DbSet<LessonSlot> LessonSlots => Set<LessonSlot>();

    // -------------------------------------------------------------------------
    // Sxema v2 — vaqt va qamrov
    // -------------------------------------------------------------------------

    /// <summary>Choraklar (I–IV). Har chorak uchun alohida <see cref="Schedule"/> bo'ladi.</summary>
    public DbSet<Term> Terms => Set<Term>();

    /// <summary>Smenalar (1-smena, 2-smena).</summary>
    public DbSet<Shift> Shifts => Set<Shift>();

    /// <summary>Dars soatlari — smenalar bo'ylab uzluksiz raqamlangan.</summary>
    public DbSet<Period> Periods => Set<Period>();

    // -------------------------------------------------------------------------
    // Sxema v2 — ma'lumotnomalar
    // -------------------------------------------------------------------------

    /// <summary>Parallellar (sinf darajalari).</summary>
    public DbSet<Grade> Grades => Set<Grade>();

    /// <summary>Sinflar (eski <see cref="ClassGroup"/> ning v2 varianti).</summary>
    public DbSet<SchoolClass> SchoolClasses => Set<SchoolClass>();

    /// <summary>Sinf bo'linishlari (butun sinf, 1/2 guruh, o'g'il/qiz).</summary>
    public DbSet<ClassDivision> ClassDivisions => Set<ClassDivision>();

    /// <summary>O'quvchilar guruhlari — dars kimga o'tilishining eng mayda birligi.</summary>
    public DbSet<StudentGroup> StudentGroups => Set<StudentGroup>();

    /// <summary>Xonalar (P1 — bo'sh bo'lishi mumkin).</summary>
    public DbSet<Classroom> Classrooms => Set<Classroom>();

    // -------------------------------------------------------------------------
    // Sxema v2 — darslar va kartochkalar (yadro)
    // -------------------------------------------------------------------------

    /// <summary>Dars ta'riflari: nima, kimga, kim, necha soat.</summary>
    public DbSet<Lesson> Lessons => Set<Lesson>();

    /// <summary>Dars ↔ o'qituvchi.</summary>
    public DbSet<LessonTeacher> LessonTeachers => Set<LessonTeacher>();

    /// <summary>Dars ↔ sinf.</summary>
    public DbSet<LessonClass> LessonClasses => Set<LessonClass>();

    /// <summary>Dars ↔ guruh.</summary>
    public DbSet<LessonGroup> LessonGroups => Set<LessonGroup>();

    /// <summary>Dars ↔ ruxsat etilgan xonalar (P1).</summary>
    public DbSet<LessonClassroom> LessonClassrooms => Set<LessonClassroom>();

    /// <summary>Kartochkalar: dars qaysi kun va soatga qo'yilgan.</summary>
    public DbSet<Card> Cards => Set<Card>();

    /// <summary>Kartochka ↔ tayinlangan xona (P1).</summary>
    public DbSet<CardClassroom> CardClassrooms => Set<CardClassroom>();

    /// <summary>Denormallashgan bandlik — DB darajasidagi to'qnashuv kafolati.</summary>
    public DbSet<CardOccurrence> CardOccurrences => Set<CardOccurrence>();

    // -------------------------------------------------------------------------
    // Sxema v2 — cheklovlar
    // -------------------------------------------------------------------------

    /// <summary>Uch holatli vaqt cheklovi matritsasi (aSc "time-off").</summary>
    public DbSet<TimeOff> TimeOffs => Set<TimeOff>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        ApplyBaseEntityConventions(modelBuilder);
        ApplySoftDeleteFilters(modelBuilder);
    }

    /// <summary>
    /// <see cref="ISoftDeletable"/> entity'lariga global so'rov filtri qo'yadi
    /// (00 §10.8, 5-band): "yumshoq o'chirilgan" yozuv oddiy so'rovlarda KO'RINMAYDI.
    /// </summary>
    /// <remarks>
    /// Filtrni chetlab o'tish kerak bo'lsa <c>IgnoreQueryFilters()</c> ishlatiladi
    /// (masalan arxiv yoki ma'lumot ko'chirish yo'llarida).
    /// <para>
    /// Filtr <b>faqat</b> <c>IsDeleted</c> ustuni bor entity'larga qo'yiladi; unikal
    /// indekslardagi <c>"IsDeleted" = 0</c> filtrlari bilan bir xil semantika.
    /// </para>
    /// </remarks>
    private static void ApplySoftDeleteFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            if (!typeof(ISoftDeletable).IsAssignableFrom(clrType)) continue;

            // e => !e.IsDeleted
            var parameter = Expression.Parameter(clrType, "e");
            var body = Expression.Not(
                Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted)));

            modelBuilder.Entity(clrType).HasQueryFilter(Expression.Lambda(body, parameter));
        }
    }

    /// <summary>
    /// <see cref="BaseEntity"/> dagi umumiy ustunlar (Uid, audit, RowVersion) barcha
    /// entity'larga bir joyda beriladi — har konfiguratsiyada takrorlanmaydi.
    /// </summary>
    private static void ApplyBaseEntityConventions(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            if (!typeof(BaseEntity).IsAssignableFrom(clrType)) continue;

            var builder = modelBuilder.Entity(clrType);
            var tableName = entityType.GetTableName() ?? clrType.Name;

            // Barqaror tashqi kalit — har jadvalda noyob.
            builder.Property(nameof(BaseEntity.Uid))
                .IsRequired()
                .ValueGeneratedNever();

            builder.HasIndex(nameof(BaseEntity.Uid))
                .IsUnique()
                .HasDatabaseName($"UX_{tableName}_Uid");

            builder.Property(nameof(BaseEntity.CreatedAtUtc)).IsRequired();
            builder.Property(nameof(BaseEntity.UpdatedAtUtc)).IsRequired(false);

            var rowVersion = builder.Property(nameof(BaseEntity.RowVersion)).IsRequired();

            // Konkurentlik tokeni faqat sxema v2 entity'larida — eski entity'larning
            // detached Update() yo'llari (EfRepository.UpdateAsync) buzilmasligi uchun.
            if (typeof(IConcurrencyAware).IsAssignableFrom(clrType))
            {
                rowVersion.IsConcurrencyToken();
            }
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Baza cheklovi buzilsa istisno <see cref="SqliteExceptionTranslator"/> orqali
    /// TIPLI variantiga o'giriladi (00 §5.4). Tipli istisnolar
    /// <see cref="DbUpdateException"/> dan meros oladi, shuning uchun mavjud
    /// <c>catch (DbUpdateException)</c> yo'llari buzilmaydi.
    /// </remarks>
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampAudit();

        try
        {
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }
        catch (DbUpdateException ex)
        {
            throw SqliteExceptionTranslator.Translate(ex);
        }
    }

    /// <inheritdoc />
    /// <remarks>Xato o'girilishi haqida <see cref="SaveChanges(bool)"/> ga qarang.</remarks>
    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        StampAudit();

        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            throw SqliteExceptionTranslator.Translate(ex);
        }
    }

    /// <summary>
    /// Audit maydonlarini to'ldiradi. Interceptor emas, kontekst ichida — chunki testlar va
    /// Desktop <c>AddDbContext</c> ni interceptorsiz ro'yxatdan o'tkazadi, bu yerda esa
    /// har qanday ro'yxatdan o'tkazishda ishlaydi.
    /// </summary>
    private void StampAudit()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.Entity.Uid == Guid.Empty) entry.Entity.Uid = Guid.NewGuid();
                    if (entry.Entity.CreatedAtUtc == default) entry.Entity.CreatedAtUtc = now;
                    entry.Entity.RowVersion = Guid.NewGuid();
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAtUtc = now;
                    entry.Entity.RowVersion = Guid.NewGuid();
                    // Uid va yaratilish payti hech qachon yangilanmaydi.
                    entry.Property(nameof(BaseEntity.CreatedAtUtc)).IsModified = false;
                    entry.Property(nameof(BaseEntity.Uid)).IsModified = false;
                    break;
            }
        }
    }
}
