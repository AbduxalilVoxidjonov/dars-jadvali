# CONTRACT-v1.md — ARXIV (WPF + `ScheduleEntry` davri)

> ⚠️ **BU HUJJAT ESKIRGAN — TARIXIY ARXIV.**
> Amaldagi shartnoma: [`CONTRACT.md`](CONTRACT.md) (**v2**).
>
> Bu fayl loyiha aSc TimeTables modeliga (sxema v2 — `Lesson` / `Card` /
> `CardOccurrence`) ko'chirilishidan **oldingi** holatni qayd etadi. Bu yerdagi
> imzolarning bir qismi hamon kodda mavjud (eski yo'l butunlay olib tashlanmagan),
> lekin **yangi ish uchun ishlatilmaydi**. Zid joyda **`CONTRACT.md` g'olib**.
>
> Eng muhim eskirgan da'volar:
>
> | Quyida yozilgan | Haqiqiy holat |
> |---|---|
> | `src/DarsJadvali.UI` (WPF) — prezentatsiya qatlami | `.sln` dan **chiqarilgan**; qatlam — `DarsJadvali.Desktop` (Avalonia) |
> | `ScheduleEntry` — asosiy jadval modeli | O'rnida `Lesson` + `Card` + `CardOccurrence` |
> | `GreedyScheduleGenerator` — yagona generator | Asosiysi `DarsJadvali.Scheduling` yadrosi (`IScheduleGenerationService`) |
> | `IUnitOfWork` — 8 ta repozitoriy, tranzaksiyasiz | `ITransactionalUnitOfWork` dan meros; `ExecuteInTransactionAsync` bor |
> | `OnDelete(DeleteBehavior.Cascade)` — eski FK'larda | Endi `Restrict` (`ScheduleEntryConfiguration`, `TeacherAssignmentConfiguration`) |
> | 8 ta `DbSet<>` | 25+ entity (sxema v2) |

---

# CONTRACT.md — Loyihaning yagona majburiy shartnomasi

> **Bu fayl HAKAM.** Barcha agentlar shu nom, imzo va namespace'larga **aynan** amal qiladi.
> Bir harf ham o'zgartirilmaydi. Yangi tur qo'shish mumkin, mavjudini o'zgartirish — YO'Q.

Solution: `DarsJadvali.sln`

```
src/DarsJadvali.Domain          net8.0          — entity, enum, konstanta
src/DarsJadvali.Application     net8.0          — abstraksiya, servis, validatsiya, generator
src/DarsJadvali.Infrastructure  net8.0          — EF Core + SQLite, repo, migration, seed
src/DarsJadvali.UI              net8.0-windows  — WPF + MaterialDesign + CommunityToolkit.Mvvm
src/DarsJadvali.Web             net8.0          — localhost test harness (minimal API + wwwroot)
tests/DarsJadvali.Tests         net8.0          — xunit
```

---

## 1. DOMAIN — `DarsJadvali.Domain`

### 1.1 `DarsJadvali.Domain.Enums.WeekDay`
```csharp
public enum WeekDay
{
    Dushanba = 1, Seshanba = 2, Chorshanba = 3, Payshanba = 4,
    Juma = 5, Shanba = 6, Yakshanba = 7
}
```

### 1.2 `DarsJadvali.Domain.Enums.WeekDayExtensions`
```csharp
public static class WeekDayExtensions
{
    public static string ToUzbek(this WeekDay day);          // "Dushanba", "Seshanba", ...
    public static IReadOnlyList<WeekDay> All { get; }         // 1..7 tartibda
}
```

### 1.3 `DarsJadvali.Domain.Common.BaseEntity`
```csharp
public abstract class BaseEntity
{
    public int Id { get; set; }
}
```

### 1.4 Entity'lar — namespace `DarsJadvali.Domain.Entities`

```csharp
public class Teacher : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string ColorCode { get; set; } = "#1976D2";
    public bool IsActive { get; set; } = true;
    public ICollection<TeacherAssignment> Assignments { get; set; } = new List<TeacherAssignment>();
    public ICollection<TeacherAvailability> Availabilities { get; set; } = new List<TeacherAvailability>();
    public ICollection<ScheduleEntry> ScheduleEntries { get; set; } = new List<ScheduleEntry>();
}

public class Subject : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string ColorCode { get; set; } = "#455A64";
    public ICollection<TeacherAssignment> Assignments { get; set; } = new List<TeacherAssignment>();
    public ICollection<ScheduleEntry> ScheduleEntries { get; set; } = new List<ScheduleEntry>();
}

public class ClassGroup : BaseEntity
{
    public string Name { get; set; } = string.Empty;      // "5-A"
    public string? RoomNumber { get; set; }                // asosiy xona
    public int StudentCount { get; set; }
    public ICollection<TeacherAssignment> Assignments { get; set; } = new List<TeacherAssignment>();
    public ICollection<ScheduleEntry> ScheduleEntries { get; set; } = new List<ScheduleEntry>();
}

public class TeacherAssignment : BaseEntity
{
    public int TeacherId { get; set; }
    public Teacher? Teacher { get; set; }
    public int SubjectId { get; set; }
    public Subject? Subject { get; set; }
    public int ClassGroupId { get; set; }
    public ClassGroup? ClassGroup { get; set; }
    public int WeeklyHoursCount { get; set; }
}

public class WorkDay : BaseEntity
{
    public WeekDay DayOfWeek { get; set; }
    public bool IsActive { get; set; } = true;
    public int MaxLessonsPerDay { get; set; } = 7;
}

public class TeacherAvailability : BaseEntity
{
    public int TeacherId { get; set; }
    public Teacher? Teacher { get; set; }
    public WeekDay DayOfWeek { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public bool IsAvailable { get; set; } = true;
}

public class ScheduleEntry : BaseEntity
{
    public int ClassGroupId { get; set; }
    public ClassGroup? ClassGroup { get; set; }
    public int SubjectId { get; set; }
    public Subject? Subject { get; set; }
    public int TeacherId { get; set; }
    public Teacher? Teacher { get; set; }
    public WeekDay DayOfWeek { get; set; }
    public int LessonNumber { get; set; }                  // 1..N
    public string? RoomNumber { get; set; }
}

// Dars soati raqamini real vaqtga bog'laydi — TeacherAvailability tekshiruvi shunga tayanadi.
public class LessonSlot : BaseEntity
{
    public int LessonNumber { get; set; }                  // 1..N, unikal
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
}
```

### 1.5 `DarsJadvali.Domain.Common.AppInfo` (Dastur haqida bo'limi uchun yagona manba)
```csharp
public static class AppInfo
{
    public const string AppName        = "Dars Jadvali Tuzuvchi";
    public const string Version        = "1.0.0";
    public const string Author         = "Abduxalil Voxidjonov";
    public const string TelegramUrl    = "https://t.me/abduxalilvoxidjonov";
    public const string TelegramHandle = "@abduxalilvoxidjonov";
    public const string DonateCardNumber = "9860 3501 4679 1495";
    public const string DonateCardType   = "Humo";
    public const string DonateCardHolder = "Abduxalil Voxidjonov";
    public const string Description = "Maktab va o'quv markazlari uchun dars jadvalini tuzish dasturi.";
}
```

---

## 2. APPLICATION — `DarsJadvali.Application`

### 2.1 `DarsJadvali.Application.Abstractions`
```csharp
public interface IRepository<T> where T : BaseEntity
{
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<T> AddAsync(T entity, CancellationToken ct = default);
    Task UpdateAsync(T entity, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task<bool> ExistsAsync(int id, CancellationToken ct = default);
}

public interface IUnitOfWork
{
    IRepository<Teacher> Teachers { get; }
    IRepository<Subject> Subjects { get; }
    IRepository<ClassGroup> ClassGroups { get; }
    IRepository<TeacherAssignment> Assignments { get; }
    IRepository<WorkDay> WorkDays { get; }
    IRepository<TeacherAvailability> Availabilities { get; }
    IRepository<ScheduleEntry> ScheduleEntries { get; }
    IRepository<LessonSlot> LessonSlots { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

// Infrastructure implement qiladi: DB yaratish/migratsiya + seed
public interface IDatabaseInitializer
{
    Task InitializeAsync(CancellationToken ct = default);
}
```

> **MUHIM:** `GetAllAsync` navigatsiyalar bilan qaytishi shart. Infrastructure buni EF Core
> `.Navigation(x => x.Teacher).AutoInclude()` orqali ta'minlaydi. Application EF Core'ni bilmaydi.

### 2.2 `DarsJadvali.Application.Validation`
```csharp
public enum ConflictSeverity { Warning = 0, Error = 1 }

public static class ConflictCodes
{
    public const string DayInactive          = "DAY_INACTIVE";
    public const string LessonOutOfRange     = "LESSON_OUT_OF_RANGE";
    public const string TeacherBusy          = "TEACHER_BUSY";
    public const string ClassBusy            = "CLASS_BUSY";
    public const string RoomBusy             = "ROOM_BUSY";
    public const string TeacherUnavailable   = "TEACHER_UNAVAILABLE";
    public const string NoAssignment         = "NO_ASSIGNMENT";
    public const string WeeklyHoursExceeded  = "WEEKLY_HOURS_EXCEEDED";
    public const string SubjectRepeatedInDay = "SUBJECT_REPEATED_IN_DAY";
    public const string TeacherInactive      = "TEACHER_INACTIVE";
}

public sealed record Conflict(ConflictSeverity Severity, string Code, string Message);

public sealed class ValidationResult
{
    public IReadOnlyList<Conflict> Conflicts { get; }
    public bool IsValid { get; }        // Error darajali konflikt YO'Q
    public bool HasWarnings { get; }
    public static ValidationResult Success();
    public static ValidationResult From(IEnumerable<Conflict> conflicts);
    public string ToDisplayText();      // har konflikt yangi qatorda, "• " prefiksi bilan
}

public sealed record ScheduleEntryDraft(
    int? Id,                 // mavjud yozuvni ko'chirayotganda uning Id'si, yangi bo'lsa null
    int ClassGroupId,
    int SubjectId,
    int TeacherId,
    WeekDay DayOfWeek,
    int LessonNumber,
    string? RoomNumber);

public interface IScheduleValidator
{
    Task<ValidationResult> ValidateAsync(ScheduleEntryDraft draft, CancellationToken ct = default);
    Task<ValidationResult> ValidateAllAsync(CancellationToken ct = default);
}
```

**`ScheduleValidator` tekshirish qoidalari (aniq, shu tartibda):**
1. `DAY_INACTIVE` (Error) — `WorkDay` da bu kun `IsActive == false` yoki umuman yo'q.
2. `LESSON_OUT_OF_RANGE` (Error) — `LessonNumber < 1` yoki `> WorkDay.MaxLessonsPerDay`.
3. `TEACHER_INACTIVE` (Error) — `Teacher.IsActive == false`.
4. `NO_ASSIGNMENT` (Error) — bu (Teacher, Subject, ClassGroup) uchligi bo'yicha `TeacherAssignment` yo'q.
5. `TEACHER_BUSY` (Error) — o'sha kun+soatda shu o'qituvchining boshqa yozuvi bor (draft.Id dan farqli).
6. `CLASS_BUSY` (Error) — o'sha kun+soatda shu sinfning boshqa yozuvi bor.
7. `ROOM_BUSY` (Error) — `RoomNumber` bo'sh emas va o'sha kun+soatda shu xona band.
8. `TEACHER_UNAVAILABLE` (Error) — `LessonSlot` orqali dars vaqti (`Start`..`End`) topiladi.
   Shu o'qituvchi + shu kun uchun `TeacherAvailability` yozuvlari **ikki xil rol** o'ynaydi:
   - **Qora ro'yxat:** biror `IsAvailable == false` oraliq bilan **kesishsa** → konflikt.
   - **Oq ro'yxat:** shu kun uchun **kamida bitta** `IsAvailable == true` oraliq bo'lsa,
     dars vaqti ulardan **bittasiga to'liq sig'ishi** shart; sig'masa → konflikt.

   Muhim: agar shu kun uchun **bironta ham** `IsAvailable == true` oraliq bo'lmasa
   (ya'ni faqat "band" oraliqlar yozilgan, yoki umuman yozuv yo'q), oq ro'yxat
   **qo'llanmaydi** — faqat qora ro'yxat ishlaydi. Ya'ni "Dushanba 09:00-11:00 band"
   deb yozish kunning qolgan soatlarini to'smaydi.

   `LessonSlot` topilmasa bu tekshiruv o'tkazib yuboriladi.

   **UI dars soati bilan ishlaydi** (`TeacherDayAvailability`, §2.4). Ya'ni foydalanuvchi
   "Dushanba: 1,2,3,4-soat" deb belgilaydi; `IAvailabilityService` uni `LessonSlot`
   vaqtlari orqali yuqoridagi oq ro'yxat yozuvlariga aylantiradi. Validatsiya dvigateli
   o'zgarmaydi — u avvalgidek vaqt oraliqlari bilan ishlaydi.
9. `WEEKLY_HOURS_EXCEEDED` (Warning) — joylashtirilgandan keyin shu biriktirma bo'yicha
   qo'yilgan soatlar `WeeklyHoursCount` dan oshib ketsa.
10. `SUBJECT_REPEATED_IN_DAY` (Warning) — shu sinfda shu fan o'sha kuni allaqachon bor.

### 2.3 `DarsJadvali.Application.Generation`
```csharp
public sealed record GenerationOptions
{
    public bool ClearExisting { get; init; } = true;
    public int MaxIterations { get; init; } = 1000;
    public int PopulationSize { get; init; } = 50;
    public double MutationRate { get; init; } = 0.05;
    public int? RandomSeed { get; init; }
}

public sealed record GenerationProgress(int Current, int Total, double Fitness, string Message);

public sealed record GenerationResult(
    bool Success,
    int PlacedCount,
    int UnplacedCount,
    IReadOnlyList<string> Messages,
    TimeSpan Elapsed);

/// Kelajakda genetik algoritm shu interfeysni implement qiladi (GeneticScheduleGenerator).
public interface IScheduleGenerator
{
    string Name { get; }
    string Description { get; }
    Task<GenerationResult> GenerateAsync(
        GenerationOptions options,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken ct = default);
}
```
Hozircha bitta ishlaydigan implementatsiya: **`GreedyScheduleGenerator`** (`Name = "Greedy (tezkor)"`).
Algoritm: barcha `TeacherAssignment` larni `WeeklyHoursCount` bo'yicha kamayish tartibida saralaydi,
har bir soat uchun (kun, dars raqami) bo'yicha `IScheduleValidator.ValidateAsync` dan o'tgan
birinchi bo'sh joyni oladi. Joy topilmasa `UnplacedCount++` va `Messages` ga izoh qo'shadi.

### 2.4 `DarsJadvali.Application.Services`
Har biri interfeys + implementatsiya (bir fayl bitta juftlik):

```csharp
public interface ITeacherService
{
    Task<IReadOnlyList<Teacher>> GetAllAsync(CancellationToken ct = default);
    Task<Teacher?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Teacher> CreateAsync(Teacher teacher, CancellationToken ct = default);
    Task UpdateAsync(Teacher teacher, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public interface ISubjectService     { /* GetAllAsync, GetByIdAsync, CreateAsync, UpdateAsync, DeleteAsync — Subject bilan */ }
public interface IClassGroupService  { /* ... ClassGroup bilan */ }

public interface IAssignmentService
{
    Task<IReadOnlyList<TeacherAssignment>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TeacherAssignment>> GetByTeacherAsync(int teacherId, CancellationToken ct = default);
    Task<IReadOnlyList<TeacherAssignment>> GetByClassGroupAsync(int classGroupId, CancellationToken ct = default);
    Task<TeacherAssignment> CreateAsync(TeacherAssignment a, CancellationToken ct = default);
    Task UpdateAsync(TeacherAssignment a, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    /// Biriktirma bo'yicha: jami soat, qo'yilgan soat, qolgan soat
    Task<(int Weekly, int Placed, int Remaining)> GetHoursSummaryAsync(int assignmentId, CancellationToken ct = default);
}

public interface IWorkDayService
{
    Task<IReadOnlyList<WorkDay>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<WorkDay>> GetActiveAsync(CancellationToken ct = default);
    Task SaveAllAsync(IEnumerable<WorkDay> days, CancellationToken ct = default);
    Task<int> GetMaxLessonNumberAsync(CancellationToken ct = default);   // faol kunlar ichidagi eng katta MaxLessonsPerDay
    Task<IReadOnlyList<LessonSlot>> GetLessonSlotsAsync(CancellationToken ct = default);
    Task SaveLessonSlotsAsync(IEnumerable<LessonSlot> slots, CancellationToken ct = default);
}

/// Bir kun uchun o'qituvchining bandligi — DARS SOATI raqamlari bilan.
/// `HasRestriction == false`  -> o'sha kuni cheklov yo'q (barcha soatlarda dars o'ta oladi).
/// `HasRestriction == true`   -> FAQAT `AllowedLessonNumbers` dagi soatlarda dars o'ta oladi.
public sealed record TeacherDayAvailability(
    WeekDay Day,
    bool HasRestriction,
    IReadOnlyList<int> AllowedLessonNumbers);

public interface IAvailabilityService
{
    Task<IReadOnlyList<TeacherAvailability>> GetByTeacherAsync(int teacherId, CancellationToken ct = default);
    Task<TeacherAvailability> CreateAsync(TeacherAvailability a, CancellationToken ct = default);
    Task UpdateAsync(TeacherAvailability a, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task ReplaceForTeacherAsync(int teacherId, IEnumerable<TeacherAvailability> items, CancellationToken ct = default);

    // --- Dars soati bo'yicha interfeys (UI shundan foydalanadi) ---

    /// Har bir FAOL ish kuni uchun bitta yozuv qaytaradi (kun tartibida).
    Task<IReadOnlyList<TeacherDayAvailability>> GetLessonAvailabilityAsync(
        int teacherId, CancellationToken ct = default);

    /// Berilgan kunlar bo'yicha bandlikni to'liq ALMASHTIRADI.
    Task SaveLessonAvailabilityAsync(
        int teacherId, IEnumerable<TeacherDayAvailability> days, CancellationToken ct = default);
}

public sealed record PlacementResult(bool Placed, ScheduleEntry? Entry, ValidationResult Validation);

public interface IScheduleService
{
    Task<IReadOnlyList<ScheduleEntry>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ScheduleEntry>> GetByClassGroupAsync(int classGroupId, CancellationToken ct = default);
    Task<IReadOnlyList<ScheduleEntry>> GetByTeacherAsync(int teacherId, CancellationToken ct = default);
    /// Validatsiyadan o'tkazadi; Error bo'lsa saqlamaydi. force=true bo'lsa Warning'larni e'tiborsiz qoldiradi (Error'ni emas).
    Task<PlacementResult> PlaceAsync(ScheduleEntryDraft draft, bool force = false, CancellationToken ct = default);
    Task<PlacementResult> MoveAsync(int entryId, WeekDay newDay, int newLessonNumber, bool force = false, CancellationToken ct = default);
    Task RemoveAsync(int entryId, CancellationToken ct = default);
    Task ClearAsync(int? classGroupId = null, CancellationToken ct = default);
}
```

### 2.5 `DarsJadvali.Application.DependencyInjection.ApplicationServiceRegistration`
```csharp
public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddApplication(this IServiceCollection services);
    // ro'yxatdan o'tkazadi: barcha *Service, IScheduleValidator -> ScheduleValidator,
    // IScheduleGenerator -> GreedyScheduleGenerator (Scoped)
}
```

---

## 3. INFRASTRUCTURE — `DarsJadvali.Infrastructure`

- `DarsJadvali.Infrastructure.Persistence.AppDbContext : DbContext` — 8 ta `DbSet<>`.
- `Persistence/Configurations/*Configuration.cs` — `IEntityTypeConfiguration<T>`, indekslar:
  - `ScheduleEntry`: unikal indeks `(ClassGroupId, DayOfWeek, LessonNumber)` va `(TeacherId, DayOfWeek, LessonNumber)`
  - `WorkDay.DayOfWeek` unikal, `LessonSlot.LessonNumber` unikal, `Subject.Code` unikal, `ClassGroup.Name` unikal
  - `TeacherAssignment`: unikal `(TeacherId, SubjectId, ClassGroupId)`
  - `TimeSpan` — SQLite uchun `long` (ticks) konverter bilan saqlanadi.
  - Navigatsiyalar `AutoInclude()` (ScheduleEntry va TeacherAssignment uchun: Teacher/Subject/ClassGroup).
  - `OnDelete(DeleteBehavior.Cascade)` — Teacher/Subject/ClassGroup o'chirilsa bog'liq yozuvlar ham.
- `Persistence/Repositories/EfRepository<T> : IRepository<T>`, `UnitOfWork : IUnitOfWork`.
- `Persistence/DatabaseInitializer : IDatabaseInitializer` — `Database.MigrateAsync()` + seed
  (7 ta `WorkDay`: Dush–Shanba faol, Yakshanba nofaol, `MaxLessonsPerDay = 7`;
   7 ta `LessonSlot`: 08:30 dan boshlab 45 daq dars + 10 daq tanaffus).
- `Migrations/` — **haqiqiy EF Core migration** (`dotnet ef migrations add InitialCreate`).
- `DarsJadvali.Infrastructure.DependencyInjection.InfrastructureServiceRegistration`:
```csharp
public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString);
    public static IServiceCollection AddInfrastructureSqlite(this IServiceCollection services, string dbFilePath);
    public static string DefaultDbPath { get; }  // %LOCALAPPDATA%/DarsJadvali/darsjadvali.db (Windows),
                                                 // aks holda ~/.local/share/DarsJadvali/darsjadvali.db
}
```
- `AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>` — `dotnet ef` uchun.

---

## 4. UI (WPF) — `DarsJadvali.UI`

- `App.xaml` / `App.xaml.cs` — `Microsoft.Extensions.Hosting` bilan DI, startda `IDatabaseInitializer`.
- Navigatsiya: `MainWindow` chap tomonda menyu, o'ngda `ContentControl` + `DataTemplate` orqali ViewModel→View.
- Sahifalar: Dashboard, O'qituvchilar, Fanlar, Sinflar, Biriktirmalar, Hafta kunlari,
  O'qituvchi vaqti, **Jadval**, **Dastur haqida**.
- "Dastur haqida" sahifasi `AppInfo` dan o'qiydi: Telegram havolasi bosiladigan,
  Humo karta raqami **9860 3501 4679 1495** va "Nusxa olish" tugmasi.

## 5. WEB (localhost test harness) — `DarsJadvali.Web`

- `dotnet run --project src/DarsJadvali.Web` → `http://localhost:5080`
- Bir xil Application/Infrastructure qatlamlaridan foydalanadi (biznes-mantiq bitta joyda).
- Minimal API `/api/...` + `wwwroot/index.html` (bitta faylli SPA, tashqi CDN'siz).

---

## 6. Umumiy qoidalar
- Barcha foydalanuvchiga ko'rinadigan matn — **o'zbek tilida** (lotin).
- `async` metodlar `CancellationToken ct = default` bilan tugaydi.
- Nullable yoqilgan; ogohlantirishlarni kamaytiring.
- Hech bir agent boshqa agentning papkasidagi faylga tegmaydi.
