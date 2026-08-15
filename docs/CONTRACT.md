# CONTRACT.md — Loyihaning yagona majburiy shartnomasi (**v2**)

> **Bu fayl HAKAM.** Barcha agentlar shu nom, imzo va namespace'larga **aynan** amal qiladi.
> Yangi tur qo'shish mumkin; mavjud imzoni o'zgartirish — **faqat shu faylni bir vaqtda
> yangilash sharti bilan**.
>
> **v1 (WPF + `ScheduleEntry` davri) arxivda:** [`CONTRACT-v1.md`](CONTRACT-v1.md).
> U yerdagi imzolarning bir qismi hamon kodda bor (eski yo'l butunlay olib
> tashlanmagan — §8 ga qarang), lekin **yangi ish uchun ishlatilmaydi**.

Solution: `DarsJadvali.sln`

```
src/DarsJadvali.Domain           net8.0   — entity, enum, konstanta
src/DarsJadvali.Scheduling       net8.0   — SOF ALGORITM YADROSI, 0 ta tashqi paket
src/DarsJadvali.Application      net8.0   — abstraksiya, servis, validatsiya, mapper
src/DarsJadvali.Infrastructure   net8.0   — EF Core + SQLite, repo, migration, chop etish
src/DarsJadvali.Desktop          net8.0   — Avalonia (ASOSIY prezentatsiya qatlami)
src/DarsJadvali.Web              net8.0   — localhost sinov serveri (minimal API + wwwroot)
tests/DarsJadvali.Tests          net8.0   — xunit
tests/DarsJadvali.Scheduling.Tests net8.0 — xunit (yadro)
```

> **`src/DarsJadvali.UI` (WPF) `.sln` dan CHIQARILGAN.** Papka diskda tarixiy nusxa
> sifatida turibdi, lekin yig'ilmaydi va unga tegilmaydi. Prezentatsiya qatlami —
> **`DarsJadvali.Desktop`**.

---

## 0. Bog'liqlik yo'nalishi — buzilmas qoida

```
Domain          ← hech kimga bog'liq emas
Scheduling      ← hech kimga bog'liq emas (Domain'ni ham ko'rmaydi)
      ↘        ↙
    Application            (Domain + Scheduling — ikkalasini ko'radigan YAGONA qatlam)
         ↑
    Infrastructure         (Application)
         ↑
  Desktop · Web            (Application + Infrastructure)
```

| Loyiha | `ProjectReference` | Asosiy paketlar |
|---|---|---|
| `Domain` | — | **yo'q** |
| `Scheduling` | — | **yo'q** (faqat BCL) |
| `Application` | `Domain`, `Scheduling` | `Microsoft.Extensions.DependencyInjection.Abstractions` 8.0.2 |
| `Infrastructure` | `Application` | `EntityFrameworkCore.Sqlite` 8.0.11, `PDFsharp` 6.2.4 |
| `Desktop` | `Application`, `Infrastructure` | Avalonia 11.2.3, Material.Avalonia 3.9.2, CommunityToolkit.Mvvm 8.3.2 |
| `Web` | `Application`, `Infrastructure` | (`Microsoft.NET.Sdk.Web`) |

**Qat'iy:** `Scheduling` yadrosiga EF Core, `Domain` entity'lari yoki UI turlari
**kirmaydi**. Yadro ↔ baza bog'lanishi faqat `Application/Scheduling/SchedulingMapper.cs`
orqali.

---

## 1. Yangi kelishuvlar (v2 da qo'shilgan) — MAJBURIY

### 1.1 `CancellationToken` qoidasi

Har bir `async` metod **oxirgi parametr** sifatida `CancellationToken ct = default`
oladi va uni pastga to'liq uzatadi. `Application` va `Infrastructure` da bu qoida
**istisnosiz** amal qiladi.

```csharp
Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
```

**Yadro (`Scheduling`) bundan farq qiladi — u istisno TASHLAMAYDI:**

```csharp
GenerationResult Generate(Problem problem, GenerationOptions options,
                          IProgress<GenerationProgress>? progress = null,
                          CancellationToken cancellationToken = default);
```

Bekor qilinganda yadro **eng yaxshi topilgan yechimni qaytaradi** va natijada
`Cancelled = true` bo'ladi. `Scheduling` loyihasida `ThrowIfCancellationRequested`
**bitta ham yo'q** — atayin. Bu "anytime" semantikasi: yuqori qatlam natijani
tashlab yubormay, foydalanuvchiga ko'rsatishi mumkin.

`ScheduleGenerationService` ham shu semantikani saqlaydi: bekor qilinganda
`OperationCanceledException` tashlamaydi, `Cancelled = true` bo'lgan hisobot qaytaradi
va **eski jadval bazada o'zgarishsiz qoladi**.

### 1.2 `IUnitOfWork` va tranzaksiya chegarasi

```csharp
// Application/Abstractions/ITransactionalUnitOfWork.cs
public interface ITransactionalUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> action, CancellationToken ct = default);

    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> action, CancellationToken ct = default);
}

// Application/Abstractions/IUnitOfWork.cs
public interface IUnitOfWork : ITransactionalUnitOfWork { /* IRepository<T> xossalari */ }
```

**Kafolatlar** (`Infrastructure/Persistence/UnitOfWork.cs`):
- **Qayta kirishga xavfsiz** — ochiq tranzaksiya bo'lsa yangisini ochmaydi, ichkarida
  to'g'ridan-to'g'ri bajaradi.
- Xatoda **rollback** qiladi va `ChangeTracker.Clear()` chaqiradi.

**Tranzaksiya chegarasi qoidasi — MAJBURIY:**

> Og'ir hisob-kitob **tranzaksiyadan TASHQARIDA** bajariladi. Tranzaksiya faqat
> **yozish** bosqichini qamrab oladi.

Sabab: SQLite yozuv qulfi butun generatsiya davomida ushlab turilsa, dasturning
qolgan qismi va Web serveri bloklanadi. Namuna
(`Application/Scheduling/ScheduleGenerationService.cs`):

```
LoadAsync            →  tranzaksiyadan TASHQARIDA (o'qish)
BuildProblem + Generate + BuildCards  →  tranzaksiyadan TASHQARIDA (sof hisob, DB yo'q)
DeleteCards + InsertCards + RebuildOccurrences  →  BITTA ExecuteInTransactionAsync ichida
```

### 1.3 Prezentatsiya uchun DTO'lar — `CardView` / `UnplacedLessonView`

**Ikkalasi ham `DarsJadvali.Application.Board` da e'lon qilinadi**
(`Application/Board/CardBoardContracts.cs`) — Desktop ham, Web ham **shu bitta**
ta'rifdan foydalanadi.

```csharp
public sealed record CardView(
    int CardId, int ScheduleId, int LessonId,
    int SubjectId, string SubjectName,
    IReadOnlyList<int> TeacherIds, IReadOnlyList<string> TeacherNames,
    IReadOnlyList<int> SchoolClassIds, string ClassName,
    IReadOnlyList<int> StudentGroupIds, string GroupName,
    int DayNo, int PeriodId, int PeriodNo,
    int Length, int WeeksMask, bool IsLocked, string? RoomNumber)
{
    public IReadOnlyList<int> ClassroomIds { get; init; }
    public bool IsDouble => Length > 1;
}

public sealed record UnplacedLessonView(
    int LessonId, int SubjectId, string SubjectName,
    string ClassName, string GroupName,
    IReadOnlyList<int> TeacherIds, IReadOnlyList<string> TeacherNames,
    int PeriodsPerWeek, int PlacedPeriods, int PeriodsPerCard);
```

**Qoida:** bu DTO'larda **UI turlari bo'lmaydi** — `IBrush`, `Color`, `Visibility` yo'q.
Rang faqat `"#RRGGBB"` **satr** sifatida uzatiladi.

### 1.4 ViewModel'lar `IBrush` qaytarmaydi (qoida **M-06**)

ViewModel **hech qachon** `IBrush` / `SolidColorBrush` / `Color` qaytarmaydi.
U **enum** yoki **satr** qaytaradi; rangni **konverter** hal qiladi.

| ViewModel nima beradi | Konverter | Natija |
|---|---|---|
| `PlacementRating` (`Forbidden`/`Allowed`/`Preferred`) | `PlacementRatingToBrushConverter` | kulrang / ko'k / yashil |
| `ConflictSeverity` (`Warning`/`Error`) | `ConflictSeverityToBrushConverter` | sariq / qizil |
| `string ColorCode` = `"#1976D2"` | `ColorCodeToBrushConverter` | `SolidColorBrush` (kesh bilan, `"Light"` parametri — ochroq variant) |

Sabab: ViewModel'ni Avalonia'siz sinash mumkin bo'lishi va rang qarorlari bitta joyda
turishi. `DarsJadvali.Application` da `IBrush`/`Avalonia.Media` **umuman yo'q**.

### 1.5 `AsyncOperationRunner` — bitta DI qamrovida bitta amal

`Desktop/Services/AsyncOperationRunner.cs`.

**Muammo:** `Application`/`Infrastructure` servislari `Scoped`, ya'ni bitta sahifaning
barcha amallari **bitta `DbContext`** ni bo'lishadi. Ikkitasi bir vaqtda ishlasa EF Core
`"A second operation was started on this context instance"` deb yiqiladi.

**Yechim — "oxirgi chaqiruv g'olib" (last-caller-wins), navbat EMAS:**

1. Yangi amal kelsa — **avvalgisi bekor qilinadi**.
2. So'ng `SemaphoreSlim(1,1)` darvozasida avvalgisi **haqiqatan tugashini** kutadi.
3. Shundan keyingina yangisi boshlanadi.

Ya'ni bir vaqtda **faqat bitta** amal ishlaydi, lekin kutayotgan amallar navbati
yig'ilmaydi — kutayotgani doim bittadan oshmaydi.

**Qayta kirish (re-entrancy):** amal ichidan yana `RunAsync` chaqirilsa,
`AsyncLocal` token orqali darvoza **qayta kutilmaydi** (aks holda deadlock bo'lardi) —
mavjud token bilan darhol bajariladi.

Sahifadan chiqishdan / DI qamrovini yopishdan oldin `CancelAndWaitAsync` chaqiriladi.

### 1.6 Tipli SQLite istisnolari

Xom `SqliteException` **UI'ga chiqmaydi**. Tarjima **yagona joyda** —
`Infrastructure/Persistence/SqliteExceptionTranslator.cs`, u
`AppDbContext.SaveChanges` va `SaveChangesAsync` ichida chaqiriladi.

```csharp
PersistenceConstraintException : DbUpdateException      // umumiy asos, ConstraintName bilan
├── UniqueConstraintViolationException                  // SQLite 2067 (UNIQUE), 1555 (PK)
├── ReferenceConstraintViolationException               // SQLite  787 (FOREIGN KEY)
└── CheckConstraintViolationException                   // SQLite  275 (CHECK), 1299 (NOT NULL)
```

**Qoidalar:**
- Baza **kodi** bo'yicha aniqlanadi (`SqliteExtendedErrorCode`), **xato matni
  parsing qilinmaydi**.
- Hammasi `DbUpdateException` dan meros oladi — mavjud `catch (DbUpdateException)`
  bloklari sinmaydi.
- Tanib olish uchun yordamchilar: `SqliteExceptionTranslator.IsUniqueViolation(ex)`,
  `.IsReferenceViolation(ex)`.

Amaliy ma'nosi: `ReferenceConstraintViolationException` — "bu yozuv ishlatilyapti,
o'chirib bo'lmaydi" (`Restrict`); `UniqueConstraintViolationException` — "bu slot
allaqachon band" (`UX_CardOccurrences_...`).

---

## 2. DOMAIN — `DarsJadvali.Domain`

### 2.1 `Common/BaseEntity`

```csharp
public abstract class BaseEntity
{
    public int Id { get; set; }
    public Guid Uid { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[]? RowVersion { get; set; }
}

public interface ISoftDeletable  { bool IsDeleted { get; set; } }
public interface IConcurrencyAware { byte[]? RowVersion { get; set; } }
```

> `CreatedAt` **emas**, `CreatedAtUtc` — `Schedule.CreatedAt` allaqachon band edi.

### 2.2 Sxema v2 entity'lari — `DarsJadvali.Domain.Entities`

| Guruh | Entity'lar |
|---|---|
| **Vaqt** | `AcademicYear`, `Term`, `Shift`, `Period`, `WorkDay` |
| **Tuzilma** | `Grade`, `SchoolClass`, `ClassDivision`, `StudentGroup`, `Classroom` |
| **Darslar** | `Lesson` + `LessonTeacher` / `LessonClass` / `LessonGroup` / `LessonClassroom` |
| **Jadval** | `Schedule`, `Card` + `CardClassroom`, **`CardOccurrence`** |
| **Cheklov** | `TimeOff` |
| **Ma'lumotnoma** | `Teacher`, `Subject` |

Asosiy kelishuvlar:

- **Chorak = alohida `Schedule` varianti.** `Schedule.TermId` +
  `Schedule.CopiedFromScheduleId`. `Card.TermsMask` / `CardOccurrence.TermNo`
  ustunlari **umuman qurilmagan**.
- **Ikki smena.** `Shift` entity; `SchoolClass.ShiftId`, `Period.ShiftId`.
  `Period.PeriodNo` smenalar bo'ylab **uzluksiz** (1-smena 1..6, 2-smena 7..12) —
  shu sababli o'qituvchining ikkala smenadagi bandligi va oyna hisobi **bitta
  o'lchovda** ko'riladi.
- **Guruhlar.** Sinfga avtomatik **5 guruh**: `tag=0` butun sinf (1),
  `tag=1` 1/2 guruh (2), `tag=2` o'g'il/qiz (2). Bandlik **guruh darajasida**
  yoziladi, sinf darajasida emas.
- **Juft dars.** `Lesson.PeriodsPerCard`, `Card.Length`.
- **A/B hafta.** `Card.WeeksMask`, `AcademicYear.WeeksInCycle`, `Schedule.WeeksInCycle`.

### 2.3 `CardOccurrence` — bandlik proyeksiyasi

To'qnashuvni **bazaning o'zi** to'sadi:

```
UNIQUE UX_CardOccurrences_Schedule_Resource_Slot
       (ScheduleId, ResourceKind, ResourceId, DayNo, PeriodNo, WeekNo)
```

`ResourceKind`: `Teacher = 1`, `StudentGroup = 2`, `Classroom = 3`.
Indeks **filtrlanmagan** — barcha qatorlarga qo'llanadi.

Yoyish qoidasi (`ICardOccurrenceProjector`):

| Dars qaysi guruhga | Bandlik kimga yoziladi | Natija |
|---|---|---|
| `IsEntireClass = true` | O'sha guruh **va sinfning barcha 5 guruhi** | "Butun sinf" + "1-guruh" bir slotda → **DB RAD ETADI** |
| Oddiy guruh | Faqat o'sha guruh | "1-guruh" + "2-guruh" bir slotda → **RUXSAT** |

**DB ushlay olmaydigan yagona holat:** turli `ClassDivision` dagi guruhlar bir slotda
("1-guruh" + "o'g'illar"). Bu Application darajasida —
`GROUP_DIVISION_OVERLAP` (`Application/Scheduling/GroupDivisionOverlapValidator.cs`).

### 2.4 `TimeOff`

```csharp
public class TimeOff : BaseEntity
{
    public int AcademicYearId { get; set; }
    public ResourceOwnerKind OwnerKind { get; set; }   // Teacher=1 … Global=7
    public int OwnerId { get; set; }                   // Global bo'lsa 0
    public int DayNo { get; set; }
    public int PeriodNo { get; set; }
    public int WeeksMask { get; set; }
    public AvailabilityLevel Availability { get; set; }
    public int Penalty { get; set; }                   // 0..1000 (CHECK bilan)

    public const int HardThreshold = 1000;
}
```

Alohida `TeacherId`/`ClassroomId` ustunlari **yo'q** — bitta polimorf
`(OwnerKind, OwnerId)` juftligi. Unikal indeks:
`UX_TimeOffs_Owner_Slot (AcademicYearId, OwnerKind, OwnerId, DayNo, PeriodNo, WeeksMask)`.

### 2.5 `Common/AppInfo`

```csharp
public static class AppInfo
{
    public const  string AppName  = "Dars Jadvali Tuzuvchi";
    public static readonly string Version;         // assembly atributidan — const EMAS
    public const  string Author   = "Abduxalil Voxidjonov";
    public const  string TelegramUrl = "https://t.me/abduxalilvoxidjonov";
    public const  string TelegramHandle = "@abduxalilvoxidjonov";
    public const  string DonateCardNumber = "9860 3501 4679 1495";
    public const  string DonateCardType   = "Humo";
    public const  string DonateCardHolder = "Abduxalil Voxidjonov";
    public const  string Description = "Maktab va o'quv markazlari uchun dars jadvalini tuzish dasturi.";
    public const  string RepositoryUrl  = "https://github.com/AbduxalilVoxidjonov/dars-jadvali";
    public const  string ReleasesUrl    = RepositoryUrl + "/releases";
    public const  string LatestReleaseUrl = ReleasesUrl + "/latest";
    public const  string ReleasesApiUrl = "https://api.github.com/repos/.../releases/latest";
    public static readonly string HttpUserAgent;
}
```

> `Version` — **`const` emas, `static readonly`**. Yagona manba —
> `Directory.Build.props` dagi `<Version>`; u assembly atributiga tushadi va shu yerda
> o'qiladi. Kodda ikkinchi marta yozilmaydi.

---

## 3. SCHEDULING (yadro) — `DarsJadvali.Scheduling`

To'liq tavsif: [`ALGORITM.md`](ALGORITM.md). Shartnoma nuqtai nazaridan majburiy:

| Element | Kelishuv |
|---|---|
| Tashqi paket | **0 ta.** `.csproj` da bitta ham `PackageReference` yo'q |
| Bog'liqlik | `Domain` ni ham **ko'rmaydi** — o'z ichki modeli (`0..N-1` zich `int` indeks) |
| `SlotMask` | `readonly struct`, 8 × `ulong` = **512 bit** |
| Chegara | `Periods ≤ 64`, `SlotCount = Weeks × DaysPerWeek × Periods ≤ 512`; oshsa `TimeGrid` konstruktori istisno tashlaydi |
| Determinizm | Bir xil `Seed` → **bayt-bayt bir xil natija**, ammo **faqat `TimeLimit` berilmagan va bekor qilinmagan bo'lsa** |
| Bekor qilish | Istisno **tashlamaydi** — §1.1 |
| Parallellik | **Yo'q** — bitta oqim, `Parallelism` sozlamasi mavjud emas |

Fazalar (`GenerationPhase`): `Verify=0`, `Propagate=1`, `Construct=2`,
`EjectionChain=3`, `Optimize=4`, `Relax=5`, `Rooms=6`, `Done=7`.

> **Tartibga diqqat:** `Rooms` enum'da `6` bo'lsa ham, **amalda `EjectionChain` bilan
> `Optimize` orasida** ishlaydi (`Scheduler.cs:125-131`).

---

## 4. APPLICATION — `DarsJadvali.Application`

### 4.1 `Abstractions/`

```csharp
public interface IRepository<T> where T : BaseEntity { /* GetAll/GetById/Add/Update/Delete/Exists */ }
public interface IUnitOfWork : ITransactionalUnitOfWork { /* §1.2 */ }
public interface IDatabaseInitializer  { Task InitializeAsync(CancellationToken ct = default); }
public interface IDatabaseBackupService { /* VACUUM INTO */ }
public interface IUpdateChecker        { Task<UpdateCheckResult> CheckAsync(CancellationToken ct = default); }
public interface ISchedulingStore      { /* LoadAsync, DeleteCardsAsync, InsertCardsAsync, LoadOccupancyAsync */ }
public interface ICardOccurrenceProjector { Task<int> RebuildForScheduleAsync(int scheduleId, CancellationToken ct = default); }
```

> `ITransactionalUnitOfWork` va `ICardOccurrenceProjector` **`Application/Abstractions/`
> da yashaydi.** `Infrastructure` dagi bir xil nomli fayllar — faqat eski chaqiruvchilar
> uchun **bo'sh o'tkazgich (shim)**, yangi kodda ishlatilmaydi.

`IUpdateChecker` **hech qanday holatda istisno tashlamaydi** — tarmoq yo'q bo'lsa ham
`UpdateStatus.Failed` qaytaradi (foydalanuvchi bekor qilgan holat bundan mustasno).

### 4.2 `Board/` — taxta bilan ishlash

`ICardBoardService` + `CardView` / `UnplacedLessonView` / `CardOccupancy` (§1.3).

### 4.3 `Scheduling/` — yadro bilan ko'prik

```
EF entity'lari
   │  ISchedulingStore.LoadAsync
   ▼
SchedulingInput
   │  ISchedulingMapper.BuildProblem      ← EF va yadro nomlari SHU YERDA uchrashadi
   ▼
Problem  ──►  Scheduler.Generate  ──►  Solution
   │  ISchedulingMapper.BuildCards
   │  SchedulingIdMap  (yadro indeksi ↔ DB Id)
   ▼
Card + CardOccurrence                     (bitta tranzaksiyada — §1.2)
```

Kirish nuqtasi — `IScheduleGenerationService.GenerateAsync`.

> **Nom to'qnashuvi — eslab qoling.** Yadrodagi `Card` — joylashtirilishi kerak bo'lgan
> **bo'lak** (joylashmagan bo'lishi mumkin). Bazadagi `Card` — **joylashtirilgan** yozuv.
> Farqni `SchedulingIdMap` yopadi.

**`TimeOff.Penalty` kelishuvi (muhim cheklov):** sonli `Penalty` yadroga
**uzatilmaydi**. U faqat **daraja tanlashda** ishlatiladi:

| Shart | Natija |
|---|---|
| `Availability == Forbidden` | `Forbidden` (taqiq) |
| `NotRecommended` va `Penalty >= TimeOff.HardThreshold` (1000) | `Forbidden` ga **ko'tariladi** |
| `NotRecommended` va `Penalty > 0` | `Questioned` — **bitta qat'iy og'irlik** (`C-AVL-06`, `w = 100`) |

Mapper ikkala holatni ham foydalanuvchiga **izoh (`Notes`) sifatida qaytaradi**, va ular
`ScheduleGenerationReport.Messages` ga tushadi.

### 4.4 `Validation/`

`ConflictCodes` — v1 dagi 10 ta kod **saqlanadi**, ustiga qo'shildi:

```csharp
public const string GroupDivisionOverlap = "GROUP_DIVISION_OVERLAP";
```

---

## 5. INFRASTRUCTURE — `DarsJadvali.Infrastructure`

| Element | Kelishuv |
|---|---|
| Provider | **SQLite**. `DefaultDbPath`: Windows `%LOCALAPPDATA%\DarsJadvali\darsjadvali.db`, macOS `~/Library/Application Support/DarsJadvali/darsjadvali.db`, Linux `~/.local/share/DarsJadvali/darsjadvali.db` |
| PRAGMA | `SqlitePragmaInterceptor` har ochilgan ulanishga: `journal_mode=WAL`, `busy_timeout=5000`, `foreign_keys=ON` |
| Migratsiya | Haqiqiy EF Core migration; `DatabaseInitializer.InitializeAsync` → `Database.MigrateAsync` |
| Zaxira | Migratsiyadan **oldin** `DatabaseBackupService` (`VACUUM INTO`) → `backups/darsjadvali-YYYYMMDD-HHMMSS.db`, oxirgi **10** tasi |
| Soft delete | `AppDbContext.ApplySoftDeleteFilters` — `ISoftDeletable` ni implement qilgan **har bir** entity'ga `e => !e.IsDeleted` global filtri |
| `OnDelete` | `Cascade` **faqat egalik zanjirlarida**; barcha ma'lumotnoma FK'lari — **`Restrict`** |
| Istisnolar | `SqliteExceptionTranslator` — §1.6 |
| Chop etish | `Export/Printing/` — **JSON dizaynlar** (`Designs/*.json`, 4 ta) + `PrintDesignPdfRenderer` (PDFsharp). Kod o'zgartirmasdan yangi dizayn qo'shiladi |

### `OnDelete` qoidasi — aniq shakl

```
Cascade  (egalik):  AcademicYear → Term/Shift/Period/Grade/SchoolClass/Classroom/
                                   Lesson/Schedule/WorkDay/TimeOff
                    Schedule     → Card, CardOccurrence, ScheduleEntry
                    Card         → CardOccurrence, CardClassroom
                    Lesson       → LessonTeacher/Class/Group/Classroom
                    SchoolClass  → ClassDivision, StudentGroup
                    Teacher      → TeacherAvailability

Restrict (ma'lumotnoma): → Teacher, Subject, Classroom, Period, Shift, Grade,
                           StudentGroup, SchoolClass, Term, ClassGroup
```

Ma'nosi: o'qituvchini o'chirish **endi butun jadvalni jimgina o'chirib yubormaydi** —
baza rad etadi va UI `ReferenceConstraintViolationException` oladi.

---

## 6. DESKTOP (Avalonia) — `DarsJadvali.Desktop`

| Element | Kelishuv |
|---|---|
| Ranglar | ViewModel `IBrush` qaytarmaydi — §1.4 |
| Bir vaqtdalik | `AsyncOperationRunner` — §1.5 |
| Navigatsiya | Har sahifa **alohida DI qamrovi (scope)** ichida; eski qamrov yangisi tayyor bo'lgach yopiladi |
| Binding | `AvaloniaUseCompiledBindingsByDefault=true` — har `UserControl`/`DataTemplate` ga **`x:DataType`** majburiy |
| ViewModel → View | `ViewLocator.cs`, nom bo'yicha: `…ViewModels.XxxViewModel` → `…Views.XxxView` |

### Jadval taxtasi — `Services/Timetable/`

| Sinf | Mas'uliyat |
|---|---|
| `TimetableBoard` | Holat + `Evaluate(...)` → `PlacementRating` (`Forbidden`/`Allowed`/`Preferred`) |
| `DragSession` | **"Karta qo'lda" (card-in-hand)** modeli — HTML5 drag-drop **EMAS**: bosib olish → kursorni yurgizish → bosib qo'yish. SHIFT — mumkin joylarni yoritish, CTRL — guruh bilan olish, ESC — bekor qilish |
| `CommandHistory` | Undo/redo, **`DefaultLimit = 100`** |
| `TimetableCommands` | `MoveCardCommand`, `SetLockCommand`, `CompositeCommand` (CTRL guruh ko'chirishi = **bitta** undo qadami) |

**Undo tarixi qachon tozalanadi:** faqat taxta bazadan **qayta yuklanganda**
(`LoadCoreAsync` → `_history.Clear()`), ya'ni sahifaga kirishda yoki "Yangilash" da.
Kartani olib qo'yish (`UnplaceCardCommand`) — **oddiy undo qilinadigan amal**, tarixni
tozalamaydi.

---

## 7. WEB — `DarsJadvali.Web`

| Element | Kelishuv |
|---|---|
| Manzil | **`http://127.0.0.1:5080`** — `localhost` emas, aynan `127.0.0.1` (tasodifan tarmoqqa ochilmasligi uchun). Boshqa manzilga o'zgartirilsa ogohlantirish yoziladi |
| Himoya | API-kalit middleware barcha `/api/*` yo'llarida (`/api/security/*` dan tashqari); yozuv metodlari (POST/PUT/PATCH/DELETE) kalitsiz **401** |
| Kalit olish | `GET /api/security/local-key` — **faqat loopback** dan |
| Rate-limit | IP bo'yicha `FixedWindowLimiter`, 1 daqiqalik oyna, `QueueLimit = 0` → darhol **429** |
| `wwwroot/index.html` | Bitta faylli SPA, **bitta ham tashqi CDN/URL yo'q** |

`/api/board` yo'llari: `axes`, `cards`, `unplaced`, `validate`, `place`, `lock`,
`generate` (POST / GET `{jobId}` / DELETE `{jobId}`), `designs`, `print`.

---

## 8. Ma'lum cheklovlar va eskirgan yo'llar — **HALOL RO'YXAT**

> Shartnoma ishlamaydigan narsani "ishlaydi" deb ko'rsatmaydi.

### 8.1 Eski model hamon tirik

**`ScheduleEntry` bazadan OLIB TASHLANMAGAN.** U hamon:
- `AppDbContext.ScheduleEntries` `DbSet` sifatida turibdi;
- `ScheduleEntryConfiguration` bilan `ScheduleEntries` jadvaliga bog'langan;
- `IUnitOfWork.ScheduleEntries`, `IScheduleService`, `ScheduleValidator`,
  `TimetableExportModelBuilder` uni ishlatadi;
- Desktop'ning **"Dars jadvali"** ekrani va Web'ning `/api/schedule/*` yo'li
  (`[Obsolete]`) unga tayanadi.

**`DropLegacyEntry` migratsiyasi YOZILMAGAN.** `V2_05` raqami esa endi band —
u `V2_05_CardLengthAndConstraints` ga berilgan.

`GreedyScheduleGenerator` `[Obsolete]` bilan belgilangan, lekin hamon kompilyatsiya
qilinadi va DI da ro'yxatdan o'tadi.

### 8.2 Yadroda amalga oshirilmagan cheklovlar

Tushlik oynasi (`C-LUN-*`), binolar va binolararo ko'chish (`C-BLD-*`),
kartalararo munosabatlar (`C-REL-*`), o'quvchi darajasidagi cheklovlar (`C-STU-*`),
A/B hafta cheklovlari (`C-CYC-02`, `C-CYC-04..07`) — **qurilmagan**.
Batafsil: [`ALGORITM.md`](ALGORITM.md) §7.

### 8.3 Boshqa aniq cheklovlar

| Cheklov | Tafsilot |
|---|---|
| `TimeOff.Penalty` | Yadroga sonli qiymat uzatilmaydi — §4.3 |
| `IConstraint.Importance` | Jarima kattaligiga ta'sir qilmaydi; `Strict` cheklovni **hard qilmaydi**, balki soft hisobdan chiqarib tashlaydi |
| `IConstraint.AllowRelaxation` | E'lon qilingan, lekin **hech qayerda o'qilmaydi** |
| `Relaxer` | Faqat **tashxis** — hech bir cheklovni o'chirmaydi, generatsiyani qayta ishga tushirmaydi |
| `TimeLimit` | Desktop UI'da **sozlanmaydi** (faqat Web API'da `TimeLimitSeconds` bor). Yadroda esa u faqat restart tsikli va `Optimizer` da tekshiriladi — bitta uzun `Construct` o'tishi chegaradan **oshib ketishi mumkin** |
| Bekor qilish kechikishi | `Verifier`, `Propagator`, `RoomAssigner`, `Relaxer` da tekshiruv **yo'q** — juda katta masalada javob shuncha kechikadi |
| Sudrab ko'chirish | **Sichqoncha bilan qo'lda sinalmagan.** Kod "karta qo'lda" modelida (§6), lekin haqiqiy sichqoncha bilan uchdan-uchgacha sinov o'tkazilmagan |
| Eski "Dars jadvali" ekrani | **Undo/redo yo'q** — o'chirish `CommandHistory` dan o'tmaydi (`TimetableViewModel` to'g'ridan-to'g'ri `_board.MoveCard(card, null)` chaqiradi) |
| 2-smena taqsimoti | Backfill barcha dars soatlarini **1-smenaga** qo'yadi; smenalarga bo'lish UI'si yo'q |

---

## 9. Umumiy qoidalar

- Barcha foydalanuvchiga ko'rinadigan matn — **o'zbek tilida** (lotin).
- `async` metodlar `CancellationToken ct = default` bilan tugaydi (§1.1).
- Nullable yoqilgan (`Directory.Build.props`: `Nullable=enable`, `LangVersion=latest`,
  `ImplicitUsings=enable`, `TreatWarningsAsErrors=false`).
- Versiya **yagona manbadan** — `Directory.Build.props` dagi `<Version>`.
- Hech bir agent boshqa agentning papkasidagi faylga tegmaydi.
