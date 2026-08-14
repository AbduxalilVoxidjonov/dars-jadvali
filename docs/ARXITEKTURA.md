# Arxitektura

**Dars Jadvali Tuzuvchi** — Clean Architecture (toza arxitektura) asosida qurilgan.
Asosiy g'oya: **biznes-mantiq texnologiyaga bog'liq emas**. Ma'lumotlar bazasini,
foydalanuvchi interfeysini yoki generatsiya algoritmini almashtirish mumkin —
qoidalar (validatsiya) o'zgarmaydi.

---

## 1. Qatlamlar va bog'liqlik yo'nalishi

```
                          ┌─────────────────────────────┐
                          │      DarsJadvali.Domain      │
                          │  Entity, Enum, AppInfo       │
                          │  (hech kimga bog'liq EMAS)   │
                          └──────────────┬──────────────┘
                                         │  ▲
                                         │  │ bog'lanadi
                                         ▼  │
                          ┌─────────────────────────────┐
                          │   DarsJadvali.Application    │
                          │  IRepository, IUnitOfWork    │
                          │  IScheduleValidator          │
                          │  IScheduleGenerator          │
                          │  I*Service                   │
                          │  (EF Core'ni BILMAYDI)       │
                          └──────────────┬──────────────┘
                                   ▲     │     ▲
              bog'lanadi ──────────┘     │     └────────── bog'lanadi
                     │                   │                    │
        ┌────────────┴──────────┐        │        ┌───────────┴────────────────────┐
        │ DarsJadvali.          │        │        │ DarsJadvali.Desktop (Avalonia) │
        │ Infrastructure        │        │        │   — asosiy, Windows + macOS    │
        │ EF Core + SQLite      │        │        │ DarsJadvali.UI (WPF) — eskirgan│
        │ EfRepository, UoW     │        │        │ DarsJadvali.Web                │
        │ Migrations, Seed      │        │        │                                │
        │ PDF eksport (PDFsharp)│        │        │ (taqdimot qatlami)             │
        └───────────────────────┘        │        └────────────────────────────────┘
                                         │
                             interfeyslar shu yerda,
                          implementatsiyalar tashqarida
```

**Bog'liqlik qoidasi — strelkalar faqat ichkariga qaraydi:**

```
Domain  ←  Application  ←  Infrastructure
                        ←  Desktop   (Avalonia — asosiy dastur)
                        ←  UI        (WPF — eskirgan)
                        ←  Web
```

| Qatlam | Nimaga bog'langan | Nimani bilmaydi |
|--------|-------------------|-----------------|
| `Domain` | hech nimaga | hamma narsani |
| `Application` | `Domain` | EF Core, SQLite, Avalonia, WPF, HTTP |
| `Infrastructure` | `Application`, `Domain` | Desktop, UI, Web |
| `Desktop` (Avalonia) | `Application`, `Infrastructure` | UI, Web |
| `UI` (WPF, eskirgan) | `Application`, `Infrastructure` | Desktop, Web |
| `Web` | `Application`, `Infrastructure` | Desktop, UI |
| `Tests` | hammasi | — |

Natija: `Infrastructure` ni butunlay almashtirish mumkin (masalan SQLite o'rniga PostgreSQL)
— `Application` kodiga bitta ham o'zgartirish kirmaydi, chunki u faqat
`IRepository<T>` va `IUnitOfWork` bilan ishlaydi.

---

## 2. Qatlamlar batafsil

### 2.1 Domain — `src/DarsJadvali.Domain`

Faqat ma'lumot tuzilmalari. Hech qanday mantiq, hech qanday NuGet paket.

- `Common/BaseEntity` — `Id` maydoni
- `Common/AppInfo` — dastur nomi, versiyasi, muallif, Telegram, donat kartasi
  (bu qiymatlar **faqat shu yerda** yoziladi, UI ham, Web ham shundan o'qiydi)
- `Enums/WeekDay` + `WeekDayExtensions.ToUzbek()` / `.All`
- `Entities/` — 8 ta entity:

```
Teacher ──┬── TeacherAssignment ──┬── Subject
          │                       └── ClassGroup
          ├── TeacherAvailability
          └── ScheduleEntry ──────┬── Subject
                                  └── ClassGroup

WorkDay      — hafta kuni faolmi, kuniga nechta dars
LessonSlot   — dars raqami ↔ aniq vaqt (08:30–09:15)
```

`LessonSlot` — muhim bo'g'in: u dars **raqamini** real **vaqtga** bog'laydi,
`TEACHER_UNAVAILABLE` tekshiruvi aynan shunga tayanadi.

### 2.2 Application — `src/DarsJadvali.Application`

Butun biznes-mantiq shu yerda.

| Papka | Nima uchun |
|-------|-----------|
| `Abstractions/` | `IRepository<T>`, `IUnitOfWork`, `IDatabaseInitializer` — bazaga "teshik" |
| `Validation/` | Konflikt kodlari, `ValidationResult`, `ScheduleEntryDraft`, `ScheduleValidator` |
| `Generation/` | `IScheduleGenerator`, `GreedyScheduleGenerator`, `GenerationOptions/Progress/Result` |
| `Export/` | `PdfExportOptions`, `ISchoolTimetablePdfExporter`, `ITimetableExportModelBuilder` — PDF uchun ma'lumot modeli (chizish Infrastructure'da) |
| `Services/` | `ITeacherService`, `ISubjectService`, ..., `IScheduleService` |
| `DependencyInjection/` | `AddApplication()` — hammasini `Scoped` qilib ro'yxatdan o'tkazadi |

### 2.3 Infrastructure — `src/DarsJadvali.Infrastructure`

EF Core + SQLite. Application'dagi interfeyslarni "to'ldiradi".

- `AppDbContext` — 8 ta `DbSet<>`
- `Configurations/` — indekslar va bog'lanishlar:
  - `ScheduleEntry` uchun ikkita **unikal** indeks:
    `(ClassGroupId, DayOfWeek, LessonNumber)` va `(TeacherId, DayOfWeek, LessonNumber)`
    — ya'ni ikki karra band bo'lish bazaning o'zida ham taqiqlangan
  - `TimeSpan` SQLite'da `long` (ticks) sifatida saqlanadi (`TimeSpanToTicksConverter`)
  - Navigatsiyalar `AutoInclude()` — `GetAllAsync()` darrov `Teacher`, `Subject`,
    `ClassGroup` bilan qaytadi, Application `Include` haqida bilmaydi
  - `OnDelete(DeleteBehavior.Cascade)` — o'qituvchi o'chirilsa uning darslari ham o'chadi
- `EfRepository<T>`, `UnitOfWork`
- `DatabaseInitializer` — `MigrateAsync()` + **idempotent** seed
  (7 kun: Dushanba–Shanba faol, Yakshanba nofaol; 7 dars soati: 08:30 dan 45+10 daqiqa)
- `Export/SchoolTimetablePdfExporter` — `ISchoolTimetablePdfExporter` implementatsiyasi:
  jadvalni PDF qilib chizadi. O'zbekcha harflar to'g'ri chiqishi uchun shrift
  dasturga qo'shib yuboriladi (`Export/Fonts/DejaVuSansCondensed*.ttf`,
  `EmbeddedFontResolver`). `AddExportServices()` uni DI ga qo'shadi.
- Baza fayli yo'li — `InfrastructureServiceRegistration.DefaultDbPath`
  (`Environment.SpecialFolder.LocalApplicationData` orqali, cross-platform):
  Windows'da `%LOCALAPPDATA%\DarsJadvali\darsjadvali.db`,
  macOS'da `~/Library/Application Support/DarsJadvali/darsjadvali.db`.

### 2.4 Desktop (Avalonia) — `src/DarsJadvali.Desktop`

**Asosiy dastur.** Avalonia 11.2.3 + Material.Avalonia + CommunityToolkit.Mvvm,
DI — `Microsoft.Extensions.Hosting`. Bitta kod bazasi **Windows'da ham, macOS'da ham**
ishlaydi (`RuntimeIdentifiers`: `osx-arm64`, `osx-x64`, `win-x64`, `win-x86`).

| Papka / fayl | Nima uchun |
|---|---|
| `Views/*.axaml` | Sahifalar (XAML emas, **AXAML**) |
| `ViewModels/` | `ViewModelBase` + sahifa ViewModel'lari, `ColorPalette` |
| `Services/` | `INavigationService`, `IDialogService` |
| `ViewLocator.cs` | ViewModel → View moslashuvi |
| `Converters/`, `Styles/`, `Models/` | Konverterlar, umumiy uslublar, kichik yordamchi modellar |

Ishga tushirish: `dotnet run --project src/DarsJadvali.Desktop`.

### 2.5 UI (WPF) — `src/DarsJadvali.UI`

**Eskirgan.** WPF + MaterialDesignThemes + CommunityToolkit.Mvvm, faqat `net8.0-windows`.
Solution ichida saqlanib turibdi, lekin yangi ish `DarsJadvali.Desktop` da olib boriladi
(WPF'dan Avalonia'ga o'tish tarixi: [`AVALONIA-KOCHIRISH.md`](AVALONIA-KOCHIRISH.md)).

### 2.6 Web — `src/DarsJadvali.Web`

`http://localhost:5080` — minimal API + bitta faylli SPA (`wwwroot/index.html`, CDN'siz).
**Biznes-mantiq takrorlanmaydi**: u ham xuddi Desktop kabi `IScheduleService`,
`IScheduleValidator`, `IScheduleGenerator` ni chaqiradi. Shuning uchun brauzerda
sinalgan xatti-harakat Desktop dasturida ham aynan shunday bo'ladi.

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

Har qanday joylashtirish (qo'lda ham, avtomatik ham) bitta yo'ldan o'tadi:

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

```csharp
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

Hozirgi implementatsiya — **`GreedyScheduleGenerator`** (`Name = "Greedy (tezkor)"`):

```
1. Barcha TeacherAssignment larni WeeklyHoursCount bo'yicha kamayish tartibida saralaydi
   (eng "og'ir" biriktirmalar avval joylashadi — ularga joy topish qiyinroq).
2. Har bir kerakli soat uchun (kun, dars raqami) juftliklarini ketma-ket sinaydi.
3. Har bir nomzod joyni IScheduleValidator.ValidateAsync dan o'tkazadi.
4. Birinchi mos joyni oladi va saqlaydi.
5. Joy topilmasa: UnplacedCount++ va Messages ga o'zbekcha izoh qo'shadi.
```

Ya'ni **generator qoidalarni o'zi bilmaydi** — u validatorga savol beradi.
Shuning uchun yangi qoida qo'shilsa, generator avtomatik unga bo'ysunadi.

### Yangi algoritm qo'shish (masalan genetik)

1. `Generation/GeneticScheduleGenerator.cs` — `IScheduleGenerator` ni implement qiling.
   `GenerationOptions` da tayyor maydonlar bor: `PopulationSize`, `MutationRate`,
   `MaxIterations`, `RandomSeed`.
2. Uzoq davom etadigan jarayonda `progress?.Report(new GenerationProgress(...))` chaqiring —
   UI progress bar'ni shundan yangilaydi. `ct.ThrowIfCancellationRequested()` ni unutmang.
3. `ApplicationServiceRegistration.AddApplication()` da ro'yxatdan o'tkazing:

```csharp
services.AddScoped<IScheduleGenerator, GreedyScheduleGenerator>();
services.AddScoped<IScheduleGenerator, GeneticScheduleGenerator>();  // yangi
```

4. Foydalanuvchiga tanlash imkonini berish uchun **Desktop** dasturidagi
   (`src/DarsJadvali.Desktop/ViewModels/DashboardViewModel.cs`) ViewModel
   `IEnumerable<IScheduleGenerator>` ni oladi va `Name` bo'yicha ro'yxat ko'rsatadi.
   Hozir "Bosh sahifa" da bitta generatorning nomi va tavsifi ko'rsatiladi.

**Muhim:** `Application`, `Infrastructure`, `Desktop` kodining qolgan qismiga tegish
shart emas. Kengaytirish nuqtasi shunga mo'ljallangan.

Boshqa kengaytirish nuqtalari xuddi shu tarzda ishlaydi:

| Kerak bo'lsa | Nimani implement qilish |
|---|---|
| Boshqa ma'lumotlar bazasi | `IRepository<T>`, `IUnitOfWork`, `IDatabaseInitializer` |
| Yangi qoida | `ScheduleValidator` ga yangi `ConflictCodes` + tekshiruv |
| Yangi algoritm | `IScheduleGenerator` |
| Boshqa eksport formati | `ISchoolTimetablePdfExporter` yonida yangi eksportchi + `ITimetableExportModelBuilder` modelidan foydalanish |
| Boshqa interfeys (web, mobil) | `I*Service` larni chaqirish |

---

## 6. Testlar

`tests/DarsJadvali.Tests` — xunit. Har bir test uchun **alohida** xotiradagi SQLite bazasi
(`TestDbFactory`, `DataSource=:memory:` + ochiq ulanish + `EnsureCreated()`),
DI konteyner esa haqiqiy `AddApplication()` bilan yig'iladi — ya'ni testlar
soxta (mock) emas, haqiqiy servislar va haqiqiy SQL ustida ishlaydi.

| Fayl | Nimani tekshiradi |
|------|-------------------|
| `ScheduleValidatorTests` | 10 ta qoidaning har biri + o'z-o'zini ko'chirish holati |
| `ScheduleServiceTests` | `PlaceAsync` / `MoveAsync` / `RemoveAsync` / `ClearAsync`, `force` mantig'i |
| `GreedyScheduleGeneratorTests` | Kichik to'plamda konfliktsiz jadval chiqishi |
| `RepositoryTests` | CRUD, cascade delete, `AutoInclude` navigatsiyalari |

```bash
dotnet test
```
