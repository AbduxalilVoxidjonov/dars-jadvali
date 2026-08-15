# Arxitektura

**Dars Jadvali Tuzuvchi** — Clean Architecture (toza arxitektura) asosida qurilgan.
Asosiy g'oya: **biznes-mantiq texnologiyaga bog'liq emas**. Ma'lumotlar bazasini,
foydalanuvchi interfeysini yoki generatsiya algoritmini almashtirish mumkin —
qoidalar (validatsiya) o'zgarmaydi.

---

## 1. Qatlamlar va bog'liqlik yo'nalishi

Loyihada **ikkita mustaqil ildiz** bor: `Domain` (ma'lumot) va `Scheduling` (algoritm).
Ular bir-birini **ko'rmaydi**; ularni birlashtiradigan yagona joy — `Application`.

```
   ┌─────────────────────────────┐     ┌──────────────────────────────────┐
   │      DarsJadvali.Domain      │     │     DarsJadvali.Scheduling       │
   │  Entity, Enum, AppInfo       │     │  SlotMask, AC-3, ejection chain, │
   │                              │     │  SA+tabu, Hopcroft–Karp          │
   │  (hech kimga bog'liq EMAS)   │     │  (0 ta tashqi paket, Domain'ni   │
   │                              │     │   ham ko'rmaydi)                 │
   └──────────────┬──────────────┘     └────────────────┬─────────────────┘
                  │                                     │
                  └──────────────┬──────────────────────┘
                                 ▼
                  ┌─────────────────────────────────────┐
                  │       DarsJadvali.Application       │
                  │  IRepository, IUnitOfWork,          │
                  │  ISchedulingStore, ICardBoardService │
                  │  ISchedulingMapper  ◄── KO'PRIK     │
                  │  (EF Core'ni ham, UI'ni ham bilmaydi)│
                  └──────────────────┬──────────────────┘
                                     ▲
                  ┌──────────────────┴──────────────────┐
                  │      DarsJadvali.Infrastructure     │
                  │  EF Core + SQLite, migratsiya,      │
                  │  backfill, projector, chop etish    │
                  └──────────────────┬──────────────────┘
                                     ▲
                  ┌──────────────────┴──────────────────┐
                  │   Desktop (Avalonia)   ·   Web      │
                  │        (taqdimot qatlami)           │
                  └─────────────────────────────────────┘
```

**Bog'liqlik qoidasi — strelkalar faqat ichkariga qaraydi:**

```
Domain      ←┐
             ├← Application ← Infrastructure ← Desktop
Scheduling  ←┘                              ← Web
```

| Qatlam | Nimaga bog'langan | Nimani bilmaydi |
|--------|-------------------|-----------------|
| `Domain` | hech nimaga | hamma narsani |
| `Scheduling` | hech nimaga | **`Domain` ni ham**, EF Core, UI |
| `Application` | `Domain`, `Scheduling` | EF Core, SQLite, Avalonia, HTTP |
| `Infrastructure` | `Application` | Desktop, Web |
| `Desktop` (Avalonia) | `Application`, `Infrastructure` | Web |
| `Web` | `Application`, `Infrastructure` | Desktop |
| `Tests` | hammasi | — |

Natija: `Infrastructure` ni butunlay almashtirish mumkin (masalan SQLite o'rniga
PostgreSQL) — `Application` kodiga bitta ham o'zgartirish kirmaydi. Xuddi shunday,
generatsiya yadrosini alohida, bazasiz va UI'siz sinash mumkin.

> **`src/DarsJadvali.UI` (WPF) `.sln` dan CHIQARILGAN.** Papka diskda tarixiy nusxa
> sifatida turibdi, lekin yig'ilmaydi va unga tegilmaydi. Prezentatsiya qatlami —
> faqat **`DarsJadvali.Desktop`** (Avalonia) va **`DarsJadvali.Web`**.

---

## 2. Qatlamlar batafsil

### 2.1 Domain — `src/DarsJadvali.Domain`

Faqat ma'lumot tuzilmalari. Hech qanday mantiq, hech qanday NuGet paket.

- `Common/BaseEntity` — `Id`, `Uid`, `CreatedAtUtc`, `UpdatedAtUtc`, `RowVersion`;
  yonida `ISoftDeletable` va `IConcurrencyAware` interfeyslari
- `Common/AppInfo` — dastur nomi, versiyasi, muallif, Telegram, donat kartasi
  (bu qiymatlar **faqat shu yerda** yoziladi, Desktop ham, Web ham shundan o'qiydi)
- `Enums/` — `WeekDay`, `ResourceKind`, `ResourceOwnerKind`, `AvailabilityLevel`, …
- `Entities/` — **sxema v2** (17 yangi + kengaytirilgan eskilari):

```
AcademicYear ─┬── Term        (chorak — HAR BIRI ALOHIDA Schedule varianti)
              ├── Shift       (smena)
              ├── Period      (dars soati; PeriodNo smenalar bo'ylab UZLUKSIZ)
              ├── Grade ── SchoolClass ─┬── ClassDivision ── StudentGroup
              │                         └── (5 guruh: butun sinf, 1/2, o'g'il/qiz)
              ├── Classroom
              ├── Lesson ──┬── LessonTeacher  ── Teacher
              │            ├── LessonClass    ── SchoolClass
              │            ├── LessonGroup    ── StudentGroup   ← BANDLIK MANBAI
              │            └── LessonClassroom── Classroom
              ├── TimeOff   (OwnerKind + OwnerId — polimorf)
              └── Schedule ──┬── Card ──┬── CardClassroom
                             │          └── CardOccurrence  ← BANDLIK PROYEKSIYASI
                             └── ScheduleEntry   (ESKI, hamon mavjud)
```

`Period` — muhim bo'g'in: u dars **raqamini** real **vaqtga** bog'laydi.

**`CardOccurrence`** — eng muhim yangilik. Bu **hosila (denormalizatsiyalangan)** jadval:
har bir kartochka o'zi band qiladigan **har bir resurs** uchun bittadan qator yozadi.
Unikal indeks to'qnashuvni **bazaning o'zida** to'sadi:

```
UNIQUE UX_CardOccurrences_Schedule_Resource_Slot
       (ScheduleId, ResourceKind, ResourceId, DayNo, PeriodNo, WeekNo)
```

Bandlik **guruh aniqligida** yoziladi — shuning uchun "1-guruh" va "2-guruh" bir slotda
tura oladi, lekin "Butun sinf" va "1-guruh" tura olmaydi.

### 2.2 Scheduling (yadro) — `src/DarsJadvali.Scheduling`

**Sof algoritm kutubxonasi.** `.csproj` da **bitta ham tashqi NuGet paketi yo'q** —
faqat BCL. EF Core ham, `Domain` entity'lari ham, UI ham unga kirmaydi.

| Papka | Nima |
|---|---|
| `Model/` | `TimeGrid` (vaqtni bitta tekis slot fazosiga yig'adi), `SlotMask` (512 bitli `readonly struct`), `Card`, `Problem`, `Solution`, `SolutionState` |
| `Constraints/` | `HardRules` (buzilmas qoidalar), `ConstraintSet` (soft qoidalar va og'irliklar) |
| `Pipeline/` | `Verifier` → `Propagator` (AC-3) → `Constructor` → `EjectionChainRepair` → `Optimizer` (SA + tabu) → `Relaxer` |
| `Rooms/` | `RoomAssigner` + `HopcroftKarp` (bipartite maksimal moslik) |
| `Util/` | `Xoshiro256SS` — determinizm manbai |

Yadro o'z ichki modeli bilan ishlaydi: barcha resurslar **`0..N-1` zich `int` indeks**,
vaqt esa bitta tekis slot fazosi. To'liq tavsif: [`ALGORITM.md`](ALGORITM.md).

### 2.3 Application — `src/DarsJadvali.Application`

Butun biznes-mantiq shu yerda. **`Domain` va `Scheduling` ni bir vaqtda ko'radigan
yagona qatlam.**

| Papka | Nima uchun |
|-------|-----------|
| `Abstractions/` | `IRepository<T>`, `IUnitOfWork`, **`ITransactionalUnitOfWork`**, `ISchedulingStore`, **`ICardOccurrenceProjector`**, `IDatabaseInitializer`, `IDatabaseBackupService`, `IUpdateChecker` |
| `Board/` | `ICardBoardService` + **`CardView`**, **`UnplacedLessonView`** — Desktop va Web uchun **umumiy** DTO'lar |
| `Scheduling/` | **`ISchedulingMapper`** (ko'prik), `ScheduleGenerationService`, `SchedulingIdMap`, `GroupDivisionOverlapValidator` |
| `Validation/` | Konflikt kodlari, `ValidationResult`, `ScheduleValidator` (eski yo'l) |
| `Generation/` | `[Obsolete]` `GreedyScheduleGenerator` — eski `ScheduleEntry` yo'li |
| `Export/` | PDF/HTML uchun ma'lumot modeli (chizish Infrastructure'da) |
| `Services/` | `ITeacherService`, …, `IScheduleService`, `IScheduleSetService` |
| `DependencyInjection/` | `AddApplication()` — hammasini `Scoped` qilib ro'yxatdan o'tkazadi |

#### Mapper — yadro va baza qayerda uchrashadi

`Application/Scheduling/SchedulingMapper.cs` — **yagona** ko'prik:

```
EF entity'lari
   │  ISchedulingStore.LoadAsync
   ▼
SchedulingInput
   │  ISchedulingMapper.BuildProblem      ← EF Id'lari → yadro indekslariga
   ▼
Problem  ──►  Scheduler.Generate  ──►  Solution
   │  ISchedulingMapper.BuildCards        ← yadro indekslari → EF Id'lariga
   │  SchedulingIdMap
   ▼
Card + CardOccurrence
```

> **Nom to'qnashuvi.** Yadrodagi `Card` — joylashtirilishi kerak bo'lgan **bo'lak**
> (joylashmagan bo'lishi mumkin). Bazadagi `Card` — **joylashtirilgan** yozuv.
> Farqni `SchedulingIdMap` yopadi.

#### Tranzaksiya chegarasi — MAJBURIY qoida

> Og'ir hisob-kitob **tranzaksiyadan TASHQARIDA**. Tranzaksiya faqat **yozish**
> bosqichini qamrab oladi.

```
LoadAsync                             →  tranzaksiyadan tashqarida (o'qish)
BuildProblem + Generate + BuildCards  →  tranzaksiyadan tashqarida (sof hisob, DB yo'q)
DeleteCards + InsertCards
   + RebuildOccurrences               →  BITTA ExecuteInTransactionAsync ichida
```

Sabab: SQLite yozuv qulfi butun generatsiya davomida ushlab turilsa, dasturning
qolgan qismi va Web serveri bloklanadi. Xato yoki bekor qilinishda **eski jadval
joyida qoladi**.

### 2.4 Infrastructure — `src/DarsJadvali.Infrastructure`

EF Core + SQLite + PDFsharp. Application'dagi interfeyslarni "to'ldiradi".

- `AppDbContext` — sxema v2 ning barcha `DbSet<>` lari (+ eski `ScheduleEntries`)
- `Configurations/` — indekslar va bog'lanishlar:
  - `UX_CardOccurrences_Schedule_Resource_Slot` — bandlikni DB darajasida to'sadi
  - `UX_Schedules_IsActive` — **filtrlangan unikal** (`"IsActive" = 1`), ya'ni
    bir vaqtda faqat bitta faol jadval
  - `TimeSpan` SQLite'da `long` (ticks) sifatida saqlanadi
  - **`OnDelete`:** `Cascade` **faqat egalik zanjirlarida**
    (`AcademicYear` → bolalari, `Schedule` → `Card`, `Lesson` → join'lari,
    `SchoolClass` → guruhlari). Ma'lumotnomalarga (`Teacher`, `Subject`, `Classroom`,
    `Period`, …) barcha FK'lar — **`Restrict`**. Ya'ni o'qituvchini o'chirish
    endi butun jadvalni jimgina o'chirib yubormaydi
  - **`HasQueryFilter`:** `AppDbContext.ApplySoftDeleteFilters` — `ISoftDeletable`
    ni implement qilgan **har bir** entity'ga `e => !e.IsDeleted` global filtri
    (`Grade`, `SchoolClass`, `StudentGroup`, `Classroom`, `Subject`, `Teacher`).
    `Card`/`CardOccurrence`/`Lesson` ataylab bundan tashqarida — yuqori hajmli
    jadvallarga ortiqcha ustun va indeks qo'shmaslik uchun
- `SqlitePragmaInterceptor` — har ochilgan ulanishga `journal_mode=WAL`,
  `busy_timeout=5000`, `foreign_keys=ON`
- **`SqliteExceptionTranslator`** — xom `SqliteException` ni tipli istisnolarga
  aylantiradi (`UniqueConstraintViolationException`,
  `ReferenceConstraintViolationException`, `CheckConstraintViolationException`).
  Yagona chaqiruv joyi — `AppDbContext.SaveChanges(Async)`
- **`DatabaseBackupService`** — `VACUUM INTO` bilan zaxira, `backups/` papkasiga,
  oxirgi 10 tasi saqlanadi
- **`Backfill/LegacyToV2Backfill`** — eski modeldan v2 ga ma'lumot ko'chirish
  (idempotent). Batafsil: [`MIGRATSIYA.md`](MIGRATSIYA.md)
- **`Projection/CardOccurrenceProjector`** — kartochkalardan bandlik qatorlarini quradi
- `DatabaseInitializer` — zaxira → `MigrateAsync()` → seed → backfill
- `Export/Printing/` — **JSON dizaynlarga asoslangan** chop etish dvigateli:
  `Designs/*.json` (4 ta) + `PrintDesignPdfRenderer` (PDFsharp) + `TimetableHtmlExporter`.
  Yangi shakl qo'shish uchun **kod o'zgartirilmaydi** — yangi JSON qo'shiladi.
  O'zbekcha harflar uchun shrift dasturga qo'shib yuboriladi
  (`Export/Fonts/DejaVuSansCondensed*.ttf`, `EmbeddedFontResolver`)
- `Update/GitHubUpdateChecker` — `IUpdateChecker` implementatsiyasi (yagona tarmoq nuqtasi)
- Baza fayli yo'li — `InfrastructureServiceRegistration.DefaultDbPath`:
  Windows'da `%LOCALAPPDATA%\DarsJadvali\darsjadvali.db`,
  macOS'da `~/Library/Application Support/DarsJadvali/darsjadvali.db`,
  Linux'da `~/.local/share/DarsJadvali/darsjadvali.db`

### 2.5 Desktop (Avalonia) — `src/DarsJadvali.Desktop`

**Asosiy va yagona ish stoli dasturi.** Avalonia 11.2.3 + Material.Avalonia +
CommunityToolkit.Mvvm, DI — `Microsoft.Extensions.Hosting`. Bitta kod bazasi
**Windows'da ham, macOS'da ham** ishlaydi
(`RuntimeIdentifiers`: `osx-arm64`, `osx-x64`, `win-x64`, `win-x86`).

| Papka / fayl | Nima uchun |
|---|---|
| `Views/*.axaml` | Sahifalar (XAML emas, **AXAML**) |
| `ViewModels/` | `ViewModelBase` + sahifa ViewModel'lari, `ColorPalette`, `TimetableMetrics` (zoom/zichlik) |
| `Services/Timetable/` | Jadval taxtasi yadrosi: `TimetableBoard` (xotiradagi holat), `TimetableRuleSet` (baholash qoidalari), `DragSession` ("qo'ldagi karta"), `CommandHistory` (undo/redo), `TimetableCommands`, `TimetableBoardWriter` (bazaga yozish), `CardViewAdapter` |
| `Services/` | `INavigationService`, `IDialogService`, `AsyncOperationRunner` |
| `ViewLocator.cs` | ViewModel → View moslashuvi |
| `Converters/`, `Styles/`, `Models/` | Konverterlar, umumiy uslublar, kichik yordamchi modellar |

**Muhim chegara:** baholash mantig'i (`TimetableBoard` + `TimetableRuleSet`) — sof
C#, Avalonia turlariga bog'liq emas va testlar bilan qoplangan
(`tests/DarsJadvali.Tests/Desktop/`). ViewModel'lar `IBrush` qaytarmaydi — rang
XAML tomonda konverter orqali tanlanadi ([`CONTRACT.md`](CONTRACT.md) §1.4).

Ishga tushirish: `dotnet run --project src/DarsJadvali.Desktop`.

### 2.6 UI (WPF) — `src/DarsJadvali.UI` — **ARXIV**

**`DarsJadvali.sln` dan CHIQARILGAN.** Papka diskda tarixiy nusxa sifatida turibdi,
lekin **yig'ilmaydi, testlanmaydi va unga tegilmaydi**. Bog'liqlik diagrammasida ham
yo'q. WPF'dan Avalonia'ga o'tish tarixi va retseptlari:
[`AVALONIA-KOCHIRISH.md`](AVALONIA-KOCHIRISH.md).

### 2.7 Web — `src/DarsJadvali.Web`

`http://127.0.0.1:5080` — minimal API + bitta faylli SPA (`wwwroot/index.html`, CDN'siz,
offline). **Biznes-mantiq takrorlanmaydi**: Desktop bilan bir xil Application
servislarini chaqiradi — jadval taxtasi uchun `ICardBoardService`, generatsiya uchun
`IScheduleGenerationService` (`/api/board/**`).

Himoya choralari (`Program.cs`):

- **Bog'lanish `127.0.0.1`** — "localhost" emas, tasodifan tarmoqqa ochilib
  qolmasligi uchun; boshqa manzilga o'tkazilsa dastur ogohlantirish yozadi
- **API kalit** — `UseApiKeyAuthorization`; kalit fayldan, sozlamadan
  (`Security:ApiKey` / `DARSJADVALI_API_KEY`) o'qiladi yoki avtomatik yaratiladi
- **Rate limiting** — `UseRateLimiter`
- Uzoq davom etadigan generatsiya — `POST /api/board/generate` **job** sifatida
  (`202 Accepted`), holati `GET /api/board/generate/{jobId}` orqali

---

## 3. MVVM oqimi (Avalonia)

```
  Foydalanuvchi
       │  (tugma bosadi, katakni tanlaydi)
       ▼
  ┌──────────┐   Binding / Command    ┌───────────────┐
  │   View   │ ─────────────────────► │   ViewModel   │
  │ (AXAML)  │ ◄───────────────────── │ (ObservableObject,
  └──────────┘   INotifyPropertyChanged│  RelayCommand)│
                                       └───────┬───────┘
                                               │ interfeys orqali
                                               ▼
                                       ┌───────────────┐
                                       │  I*Service    │  ← Application
                                       │  IScheduleService, IScheduleValidator ...
                                       └───────┬───────┘
                                               ▼
                                       ┌───────────────┐
                                       │  IUnitOfWork  │  ← Application abstraksiyasi
                                       └───────┬───────┘
                                               ▼
                                       ┌───────────────┐
                                       │ EfRepository  │  ← Infrastructure
                                       │  + SQLite     │
                                       └───────────────┘
```

- `View` — faqat ko'rinish. Kod-behind'da mantiq yo'q.
- `ViewModel` — holat va buyruqlar (`[ObservableProperty]`, `[RelayCommand]`).
  Servislarni **konstruktor orqali** oladi (DI).
- `MainWindow.axaml` — chapda menyu (`MainViewModel.MenuItems`), o'ngda
  `ContentControl Content="{Binding CurrentViewModel}"`.
- ViewModel → View moslashuvi **nom bo'yicha**, `ViewLocator.cs` orqali:
  `…ViewModels.XxxViewModel` → `…Views.XxxView`. `ViewLocator` `App.axaml` dagi
  `Application.DataTemplates` ga qo'shilgan, shuning uchun alohida `DataTemplate`
  ro'yxati (WPF'dagi `Resources/ViewTemplates.xaml` kabi fayl) **kerak emas**.
- Sahifadan sahifaga o'tish — `Services/NavigationService.cs`: har bir sahifa
  **alohida DI qamrovi (scope)** ichida yaratiladi, ya'ni har sahifa yangi
  `DbContext` bilan ishlaydi va eski ma'lumot qolib ketmaydi. Eski qamrov
  yangisi tayyor bo'lgach yopiladi.
- ViewModel EF Core'ni ham, `AppDbContext` ni ham ko'rmaydi.

---

## 4. Validatsiya dvigateli qanday ishlaydi

> **Diqqat — endi uchta tekshiruv qatlami bor:**
>
> | Qatlam | Nima | Qayerda |
> |---|---|---|
> | **1. Baza** | `UX_CardOccurrences_...` unikal indeksi bandlikni **guruh aniqligida** to'sadi | `CardOccurrenceConfiguration` |
> | **2. Application** | `GROUP_DIVISION_OVERLAP` — DB ushlay olmaydigan yagona holat (turli bo'linishdagi guruhlar bir slotda) | `GroupDivisionOverlapValidator` |
> | **3. Yadro** | Hard/soft cheklovlar — avtomatik tuzishda | `Scheduling/Constraints/` |
>
> Quyidagi `ScheduleValidator` — **eski (`ScheduleEntry`) yo'lining** validatori.
> U hamon ishlab turibdi (chap menyudagi "Dars jadvali" ekrani va `/api/schedule/*`
> unga tayanadi), lekin yangi jadval taxtasi va avtomatik tuzish undan o'tmaydi.

Eski yo'lda har qanday joylashtirish bitta yo'ldan o'tadi:

```
ScheduleEntryDraft (Id?, Class, Subject, Teacher, Day, LessonNumber, Room)
        │
        ▼
  IScheduleValidator.ValidateAsync(draft)
        │
        │  1. DAY_INACTIVE           (Error)   ← WorkDay.IsActive
        │  2. LESSON_OUT_OF_RANGE    (Error)   ← WorkDay.MaxLessonsPerDay
        │  3. TEACHER_INACTIVE       (Error)   ← Teacher.IsActive
        │  4. NO_ASSIGNMENT          (Error)   ← TeacherAssignment (3 lik)
        │  5. TEACHER_BUSY           (Error)   ← boshqa ScheduleEntry (draft.Id dan farqli)
        │  6. CLASS_BUSY             (Error)
        │  7. ROOM_BUSY              (Error)
        │  8. TEACHER_UNAVAILABLE    (Error)   ← LessonSlot + TeacherAvailability
        │  9. WEEKLY_HOURS_EXCEEDED  (Warning) ← TeacherAssignment.WeeklyHoursCount
        │ 10. SUBJECT_REPEATED_IN_DAY(Warning)
        ▼
  ValidationResult { Conflicts[], IsValid, HasWarnings, ToDisplayText() }
        │
        ▼
  IScheduleService.PlaceAsync(draft, force)
        │
        ├── Error bor            → SAQLAMAYDI (force ham yordam bermaydi)
        ├── faqat Warning, force=false → SAQLAMAYDI, ogohlantirish qaytaradi
        └── faqat Warning, force=true  → SAQLAYDI
```

### `draft.Id` nima uchun kerak

Mavjud darsni **ko'chirayotganda** draft ichida uning `Id` si beriladi.
Validator band-bandlikni tekshirishda **shu Id'li yozuvni hisobga olmaydi** —
aks holda dars "o'zi bilan o'zi" konflikt berardi (`TEACHER_BUSY`).
Bu holat testlar bilan qopqoqlangan (`Ozini_ozi_kochirganda_TEACHER_BUSY_bermaydi`).

### `TEACHER_UNAVAILABLE` mantig'i

`LessonSlot` orqali dars raqami aniq vaqtga (`Start`..`End`) aylantiriladi.
Shu o'qituvchi + shu kun uchun `TeacherAvailability` yozuvlari **ikki xil rol** o'ynaydi:

- **Qora ro'yxat** — biror `IsAvailable == false` oraliq bilan **kesishsa** → konflikt.
- **Oq ro'yxat** — shu kun uchun **kamida bitta** `IsAvailable == true` oraliq bo'lsa,
  dars vaqti ulardan **bittasiga to'liq sig'ishi** shart.

Agar shu kun uchun bironta ham `IsAvailable == true` oraliq bo'lmasa
(faqat "band" oraliqlar yozilgan yoki umuman yozuv yo'q), oq ro'yxat qo'llanmaydi —
ya'ni "Dushanba 09:00–11:00 band" deb yozish kunning qolgan soatlarini to'smaydi.
`LessonSlot` topilmasa bu tekshiruv o'tkazib yuboriladi.

**UI esa vaqt bilan emas, dars soati raqamlari bilan ishlaydi.** "O'qituvchi vaqti"
ekranida har bir kun uchun "Cheklov bor" belgisi va ruxsat etilgan soat raqamlari
tanlanadi (`TeacherDayAvailability`); `IAvailabilityService.SaveLessonAvailabilityAsync`
uni `LessonSlot` vaqtlari orqali yuqoridagi oq/qora ro'yxat yozuvlariga aylantiradi.
Validatsiya dvigateli o'zgarmaydi — u avvalgidek vaqt oraliqlari bilan ishlaydi.

### `ValidateAllAsync()`

Butun mavjud jadvalni qayta tekshiradi (masalan o'qituvchi nofaol qilingandan yoki
hafta kuni o'chirilgandan keyin). Avtomatik generatsiya natijasini tekshirishda ham
shu ishlatiladi.

---

## 5. Generatsiya va kengaytirish nuqtasi

Avtomatik tuzish **`DarsJadvali.Scheduling`** yadrosida bajariladi. Kirish nuqtasi —
`IScheduleGenerationService.GenerateAsync` (`Application/Scheduling/`).

```
DashboardViewModel  (Seed, Complexity, KeepLocked, SavePartial)
        │  IProgress<ScheduleGenerationProgress>  +  CancellationToken
        ▼
IScheduleGenerationService.GenerateAsync
        │  1. ISchedulingStore.LoadAsync         ← tranzaksiyadan tashqarida
        │  2. ISchedulingMapper.BuildProblem     ← EF Id → yadro indeksi
        │  3. Scheduler.Generate                 ← 6 faza, sof hisob, DB yo'q
        │  4. ISchedulingMapper.BuildCards       ← yadro indeksi → EF Id
        │  5. GroupDivisionOverlapValidator.Check
        │  6. DeleteCards + InsertCards + RebuildOccurrences
        │                                        ← BITTA tranzaksiyada
        ▼
ScheduleGenerationReport (Placed, Unplaced, SoftCost, Cancelled, Messages)
```

**Bekor qilish semantikasi:** istisno tashlanmaydi. Bekor qilinganda hisobotda
`Cancelled = true` bo'ladi va **eski jadval bazada o'zgarishsiz qoladi** — chunki
yozish bosqichiga umuman yetib borilmaydi.

> **Eski yo'l.** `IScheduleGenerator` / `GreedyScheduleGenerator`
> (`Name = "Greedy (tezkor)"`) hamon kompilyatsiya qilinadi va DI da ro'yxatdan
> o'tadi, lekin **`[Obsolete]`** bilan belgilangan: u eski `ScheduleEntry` modeli
> ustida ishlaydi. Yangi ish uchun ishlatilmaydi.

### Kengaytirish nuqtalari

| Kerak bo'lsa | Nimani o'zgartirish |
|---|---|
| **Yangi soft qoida** (jarima bilan) | `Scheduling/Constraints/` da `ConstraintBase` dan meros olib yangi sinf yozing, so'ng uni `ConstraintSet.CreateDefault()` ga qo'shing |
| **Og'irlikni sozlash** | `ConstraintSet.CreateDefault()` dagi `Weight` — [`ALGORITM.md`](ALGORITM.md) §3.2 |
| **Yangi hard qoida** | `Scheduling/Constraints/HardRules.cs` + `SolutionState.TryApply` invariantlari |
| **Yadro ↔ baza bog'lanishi** | `Application/Scheduling/SchedulingMapper.cs` — **yagona** ko'prik |
| Boshqa ma'lumotlar bazasi | `IRepository<T>`, `IUnitOfWork`, `ISchedulingStore`, `ICardOccurrenceProjector` |
| Qo'lda joylashtirish qoidasi | `ScheduleValidator` ga yangi `ConflictCodes` + tekshiruv |
| **Yangi chop etish shakli** | `Infrastructure/Export/Printing/Designs/` ga yangi **JSON** qo'shing — kod o'zgartirilmaydi |
| Boshqa interfeys (web, mobil) | `ICardBoardService`, `IScheduleGenerationService` ni chaqiring |

Yadro **hech qanday tashqi paketga bog'liq emas va EF Core'ni ko'rmaydi** — shuning
uchun uni bazasiz, UI'siz, alohida sinash mumkin.

---

## 6. Testlar

Ikkita test loyihasi bor.

### `tests/DarsJadvali.Tests`

Har bir test uchun **alohida** xotiradagi SQLite bazasi
(`TestDbFactory`, `DataSource=:memory:` + ochiq ulanish), DI konteyner esa haqiqiy
`AddApplication()` bilan yig'iladi — ya'ni testlar soxta (mock) emas, haqiqiy
servislar va haqiqiy SQL ustida ishlaydi.

| Fayl | Nimani tekshiradi |
|------|-------------------|
| `ScheduleValidatorTests` | 10 ta qoidaning har biri + o'z-o'zini ko'chirish holati |
| `ScheduleServiceTests` | `PlaceAsync` / `MoveAsync` / `RemoveAsync` / `ClearAsync`, `force` mantig'i |
| `ScheduleSetServiceTests` | Jadval variantlari, `SetActiveAsync` (tranzaksiya + filtrlangan UNIQUE) |
| `AcademicYearServiceTests` | O'quv yili bilan ishlash |
| `LessonAvailabilityTests` | Dars soati bo'yicha o'qituvchi bandligi |
| `DatabaseMigrationTests` | Migratsiyalar va eski ma'lumotni ko'chirish (`LegacyToV2Backfill`) |
| `RepositoryTests` | CRUD, o'chirish xatti-harakati, `AutoInclude` navigatsiyalari |
| `PdfExportTests` | Chop etish dvigateli |
| `UpdateCheckerTests` | Yangilanishni tekshirish (tarmoqsiz, soxta javob bilan) |
| `GreedyScheduleGeneratorTests` | Eski generator (arxiv qamrovi) |

### `tests/DarsJadvali.Scheduling.Tests`

Yadro testlari — **bazasiz va UI'siz**, sof hisob ustida.

| Fayl | Nimani tekshiradi |
|------|-------------------|
| `SlotMaskTests`, `DayBitsTests` | Bitset amallari |
| `HardConstraintTests` | Hard qoidalar hech qachon buzilmasligi |
| `DeltaConsistencyTests` | Inkremental jarima hisobi to'liq qayta hisob bilan mos kelishi |
| `DivisionTagTests` | Guruh bo'linishi mantiqi |
| `RoomTests` | Hopcroft–Karp xona taqsimlash |
| `DeterminismTests` | Bir xil `Seed` → **bayt-bayt bir xil** natija |
| `CancellationTests` | Bekor qilinganda istisno emas, `Cancelled = true` |
| `VerifierRelaxerTests` | Tashxis fazalari |
| `IntegrationTests` | To'liq pipeline |
| `BenchmarkTests` | 30 sinf × 150 guruh × 1170 karta — §6 [`ALGORITM.md`](ALGORITM.md) |

```bash
dotnet test

# To'liq benchmark o'lchovi (uzoq davom etadi)
DJ_BENCH=1 dotnet test --filter Category=Benchmark
```

---

## 7. Ma'lum cheklovlar

> Arxitektura hujjati ishlamaydigan narsani "ishlaydi" deb ko'rsatmaydi.

### 7.1 Ikkita model yonma-yon

Eski (`ScheduleEntry`) va yangi (`Lesson`/`Card`/`CardOccurrence`) modellar **bir vaqtda
tirik**. Bu ataylab: 1-bosqich additiv bo'lgan, foydalanuvchi ma'lumoti yo'qolmasligi
uchun. Amaldagi natija:

| Nima | Holat |
|---|---|
| `ScheduleEntry` entity, `DbSet` va jadval | **Mavjud** (`AppDbContext.cs:28`) |
| `DropLegacyEntry` migratsiyasi | **Yozilmagan.** `V2_05` raqami esa band (`V2_05_CardLengthAndConstraints`) |
| `IScheduleService`, `ScheduleValidator`, `TimetableExportModelBuilder` | Eski model ustida ishlaydi |
| `GreedyScheduleGenerator` | `[Obsolete]`, lekin kompilyatsiya qilinadi va DI da |
| Desktop "Dars jadvali" ekrani, Web `/api/schedule/*` | Eski modelga tayanadi |

Ya'ni bitta mantiq ikki joyda: bu **texnik qarz**, keyingi bosqichda yopiladi.

### 7.2 Prezentatsiyada ikkita jadval ekrani

| Ekran | ViewModel | Undo/redo |
|---|---|---|
| **Bosh sahifa** dagi jadval taxtasi | `TimetableBoardViewModel` | **Bor** (100 qadam) |
| Chap menyudagi **"Dars jadvali"** | `TimetableViewModel` | **Yo'q** — `CommandHistory` dan o'tmaydi |

### 7.3 Sinalmagan joylar

- **Sudrab ko'chirish sichqoncha bilan qo'lda sinalmagan.** Mexanika (`DragSession`)
  yozilgan va mantiqiy testlar bor, lekin uchdan-uchgacha qo'lda sinov o'tkazilmagan.
- `V2_05`–`V2_07` migratsiyalarining `Down()` metodlari to'liq oldinga/orqaga
  aylanish sinovidan o'tkazilmagan (`V2_01`–`V2_04` o'tkazilgan).

### 7.4 Yadroda qurilmagan cheklovlar

Tushlik oynasi (`C-LUN-*`), binolar va binolararo ko'chish (`C-BLD-*`),
kartalararo munosabatlar (`C-REL-*`), o'quvchi darajasidagi cheklovlar (`C-STU-*`),
A/B hafta cheklovlari. Shuningdek `TimeOff.Penalty` yadroga sonli qiymat sifatida
uzatilmaydi. Batafsil: [`ALGORITM.md`](ALGORITM.md) §7.

### 7.5 Boshqa

- **2-smena taqsimoti:** `Shift` entity va smena filtri bor, lekin eski bazadan
  ko'chirishda barcha dars soatlari **1-smenaga** tushadi; taqsimlash UI'si yo'q.
- **Parallellik:** yadro bitta oqimda ishlaydi (`Parallelism` sozlamasi yo'q).
- **Yangilanishni tekshirish** — dasturdagi yagona tarmoq nuqtasi ("Dastur haqida"
  sahifasi ochilganda fon rejimida `github.com` ga so'rov yuboriladi).

---

Tegishli hujjatlar:
[`CONTRACT.md`](CONTRACT.md) · [`ALGORITM.md`](ALGORITM.md) ·
[`MIGRATSIYA.md`](MIGRATSIYA.md) · [`FOYDALANISH.md`](FOYDALANISH.md)
