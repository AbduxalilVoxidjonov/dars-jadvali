# Dars Jadvali Tuzuvchi

Maktab va o'quv markazlari uchun **dars jadvalini tuzish** dasturi.
**Windows va macOS** uchun ish stoli dasturi (Avalonia), ma'lumotlar bazasi — SQLite
(internet talab qilinmaydi).

Jadvalni qo'lda ham, bir tugma bilan avtomatik ham tuzish mumkin.
Har bir joylashtirish **10 ta qoida** bo'yicha tekshiriladi: o'qituvchi bandmi, sinf bandmi,
xona bandmi, o'qituvchining ish vaqti mos keladimi va hokazo.

---

## Imkoniyatlar

- **O'qituvchilar** — F.I.Sh., telefon, rang (jadvalda ajratib ko'rsatish uchun), faol/nofaol holat
- **Fanlar** — nomi, qisqa kodi, rangi
- **Avtomatik rang tanlash** — yangi fan yoki o'qituvchi qo'shilganda paletkadan hali
  ishlatilmagan rang o'zi tanlanadi (qo'lda o'zgartirish mumkin)
- **Sinflar** — nomi (`5-A`), asosiy xonasi, o'quvchilar soni
- **Biriktirmalar** — kim, qaysi fandan, qaysi sinfda, haftasiga necha soat
- **Hafta kunlari** — qaysi kunlar ish kuni, kuniga nechta dars
- **Dars soatlari** — har bir dars raqamining aniq vaqti (08:30–09:15 va h.k.)
- **O'qituvchi vaqti** — kim qaysi kuni, qaysi soatlar oralig'ida ishlay oladi
- **Jadval** — qo'lda joylashtirish, ko'chirish, o'chirish; konfliktlar darhol ko'rinadi
- **Avtomatik generatsiya** — `Greedy (tezkor)` algoritmi butun jadvalni o'zi tuzadi
- **Validatsiya** — 8 ta Error (joylashtirishga yo'l qo'ymaydi) + 2 ta Warning (ogohlantiradi)
- **PDF eksport** — tayyor jadvalni bir sinf uchun yoki barcha sinflar bo'yicha PDF qilib
  saqlash ("PDF yuklab olish" tugmasi)
- **Localhost sinov rejimi** — brauzerda, istalgan operatsion tizimda sinab ko'rish

### Tekshiriladigan qoidalar

| Kod | Daraja | Ma'nosi |
|-----|--------|---------|
| `DAY_INACTIVE` | Xato | Bu kun ish kuni emas |
| `LESSON_OUT_OF_RANGE` | Xato | Dars raqami ruxsat etilgan oraliqdan tashqarida |
| `TEACHER_INACTIVE` | Xato | O'qituvchi faol emas |
| `NO_ASSIGNMENT` | Xato | Bu o'qituvchi bu sinfda bu fandan dars bermaydi |
| `TEACHER_BUSY` | Xato | O'qituvchi shu vaqtda band |
| `CLASS_BUSY` | Xato | Sinfda shu vaqtda boshqa dars bor |
| `ROOM_BUSY` | Xato | Xona shu vaqtda band |
| `TEACHER_UNAVAILABLE` | Xato | O'qituvchi shu vaqtda ishlamaydi |
| `WEEKLY_HOURS_EXCEEDED` | Ogohlantirish | Haftalik soat me'yoridan oshdi |
| `SUBJECT_REPEATED_IN_DAY` | Ogohlantirish | Bu fan shu sinfda shu kuni allaqachon bor |

---

## Talablar

| | |
|---|---|
| Operatsion tizim | **Windows 10 / 11** (x64 yoki x86) yoki **macOS 11 (Big Sur)** va undan yangi (Apple Silicon yoki Intel) |
| Ish vaqti muhiti | **.NET 8 Runtime** — [yuklab olish](https://dotnet.microsoft.com/download/dotnet/8.0/runtime) |
| Yoki | **self-contained** versiya — hech narsa o'rnatish shart emas |
| Dasturchi uchun | **.NET 8 SDK** — [yuklab olish](https://dotnet.microsoft.com/download/dotnet/8.0) |

> **Diqqat:** dastur Avalonia'da yozilgani uchun oddiy **`.NET Runtime`** yetarli —
> **`.NET Desktop Runtime` shart emas**. Desktop Runtime o'rnatilgan bo'lsa ham ishlaydi.
> Batafsil: [`docs/CHIQARISH.md`](docs/CHIQARISH.md).

> Localhost sinov rejimi (`DarsJadvali.Web`) **macOS va Linux'da ham** ishlaydi —
> u yerda faqat .NET 8 SDK kerak.

---

## Loyiha strukturasi

```
darsjadvali/
├── DarsJadvali.sln                  # Solution
├── Directory.Build.props            # Umumiy sozlamalar (net8.0, nullable, versiya)
├── README.md
├── .gitignore
│
├── src/
│   ├── DarsJadvali.Domain/          # net8.0 — entity, enum, konstanta (hech kimga bog'liq emas)
│   │   ├── Common/                  #   BaseEntity, AppInfo
│   │   ├── Entities/                #   Teacher, Subject, ClassGroup, TeacherAssignment,
│   │   │                            #   WorkDay, TeacherAvailability, ScheduleEntry, LessonSlot
│   │   └── Enums/                   #   WeekDay + WeekDayExtensions
│   │
│   ├── DarsJadvali.Application/     # net8.0 — biznes-mantiq (EF Core'ni bilmaydi)
│   │   ├── Abstractions/            #   IRepository<T>, IUnitOfWork, IDatabaseInitializer
│   │   ├── Validation/              #   IScheduleValidator, ScheduleValidator, Conflict, ValidationResult
│   │   ├── Generation/              #   IScheduleGenerator, GreedyScheduleGenerator, GenerationOptions
│   │   ├── Export/                  #   PdfExportOptions, ISchoolTimetablePdfExporter,
│   │   │                            #   ITimetableExportModelBuilder, TimetableExportModel
│   │   ├── Services/                #   ITeacherService, IScheduleService, ...
│   │   └── DependencyInjection/     #   AddApplication()
│   │
│   ├── DarsJadvali.Infrastructure/  # net8.0 — EF Core + SQLite
│   │   ├── Persistence/             #   AppDbContext, UnitOfWork, DatabaseInitializer
│   │   │   ├── Configurations/      #   IEntityTypeConfiguration<T>, indekslar, AutoInclude
│   │   │   ├── Converters/          #   TimeSpan -> long (ticks)
│   │   │   └── Repositories/        #   EfRepository<T>
│   │   ├── Migrations/              #   EF Core migratsiyalari
│   │   ├── Export/                  #   SchoolTimetablePdfExporter (PDF chizish), shrift fayllari
│   │   └── DependencyInjection/     #   AddInfrastructure(), AddInfrastructureSqlite()
│   │
│   ├── DarsJadvali.Desktop/         # net8.0 — ASOSIY dastur: Avalonia + Material.Avalonia + MVVM
│   │   ├── Views/                   #   .axaml sahifalar (Windows va macOS uchun bitta kod)
│   │   ├── ViewModels/              #   CommunityToolkit.Mvvm + ColorPalette
│   │   ├── Converters/, Services/, Styles/, Models/
│   │   ├── ViewLocator.cs           #   ViewModel → View moslashuvi
│   │   └── App.axaml.cs             #   DI (Microsoft.Extensions.Hosting) + baza init
│   │
│   ├── DarsJadvali.UI/              # net8.0-windows — ESKI WPF versiyasi (o'rniga Desktop ishlatiladi)
│   │   ├── Views/                   #   XAML sahifalar
│   │   ├── ViewModels/              #   CommunityToolkit.Mvvm
│   │   ├── Converters/, Services/, Resources/
│   │   └── App.xaml.cs              #   DI (Microsoft.Extensions.Hosting) + baza init
│   │
│   └── DarsJadvali.Web/             # net8.0 — localhost sinov serveri (minimal API + wwwroot)
│       ├── Endpoints/               #   /api/... 
│       └── Dtos/
│
├── tests/
│   └── DarsJadvali.Tests/           # net8.0 — xunit
│       ├── TestDbFactory.cs         #   izolyatsiyalangan SQLite in-memory baza
│       ├── ScheduleValidatorTests.cs
│       ├── ScheduleServiceTests.cs
│       ├── GreedyScheduleGeneratorTests.cs
│       └── RepositoryTests.cs
│
├── build/
│   ├── publish-macos.sh             # macOS relizi: .app bundle + DMG (arm64 / x64)
│   ├── publish-windows.ps1          # Windows relizi: .exe + ZIP (x64 / x86)
│   ├── sign-macos.sh                # macOS imzolash va notarizatsiya (sertifikat bo'lsa)
│   ├── Info.plist.template          # .app bundle uchun Info.plist shabloni
│   ├── publish.ps1                  # ESKI: WPF versiyasini yig'adi
│   ├── publish.bat                  # ESKI: publish.ps1 uchun oddiy wrapper
│   ├── run-web.ps1                  # localhost server (Windows)
│   ├── run-web.sh                   # localhost server (macOS/Linux)
│   └── README.md
│
└── docs/
    ├── CONTRACT.md                  # Qatlamlar orasidagi shartnoma (imzolar)
    ├── ARXITEKTURA.md               # Arxitektura va kengaytirish nuqtalari
    ├── FOYDALANISH.md               # Foydalanuvchi uchun qadamma-qadam qo'llanma
    ├── CHIQARISH.md                 # Reliz chiqarish (Windows va macOS)
    └── AVALONIA-KOCHIRISH.md        # WPF'dan Avalonia'ga o'tish tarixi
```

---

## Ishga tushirish

### a) Asosiy dastur — Windows yoki macOS

```bash
dotnet run --project src/DarsJadvali.Desktop
```

Birinchi ishga tushirishda ma'lumotlar bazasi avtomatik yaratiladi va
hafta kunlari (Dushanba–Shanba faol) hamda 7 ta dars soati bilan to'ldiriladi.

### b) Localhostda sinash — har qanday OS (Windows, macOS, Linux)

```bash
dotnet run --project src/DarsJadvali.Web
```

So'ng brauzerda oching:

**http://localhost:5080**

Yoki tayyor skript bilan:

```powershell
.\build\run-web.ps1     # Windows
```

```bash
bash build/run-web.sh   # macOS / Linux
```

Bu rejim Desktop dasturi bilan **aynan bir xil** Application va Infrastructure qatlamlarini
ishlatadi — ya'ni validatsiya va generatsiya mantig'i bir xil ishlaydi.

---

## Build qilish (tarqatish uchun)

Ikkala platforma ham **bitta manba koddan** (`src/DarsJadvali.Desktop`) yig'iladi,
lekin har biri o'z kompyuterida: macOS relizi Mac'da, Windows relizi Windows'da.

### macOS

```bash
bash build/publish-macos.sh                 # arm64 va x64 — .app + DMG
bash build/publish-macos.sh --arch arm64    # faqat Apple Silicon
bash build/publish-macos.sh --no-dmg        # DMG'siz, faqat .app (sinov uchun)
```

Natija `publish/` papkasida: `osx-arm64/DarsJadvali.app`, `osx-x64/DarsJadvali.app`
va tarqatiladigan `DarsJadvali-1.0.0-macos-arm64.dmg`, `DarsJadvali-1.0.0-macos-x64.dmg`.

### Windows

```powershell
.\build\publish-windows.ps1                       # x64 va x86, self-contained
.\build\publish-windows.ps1 -Runtime win-x86      # faqat 32-bitli Windows
.\build\publish-windows.ps1 -SelfContained $false # kichik hajm, .NET 8 Runtime alohida kerak
```

Natija `publish/` papkasida: `win-x64/DarsJadvali.exe`, `win-x86/DarsJadvali.exe`
va ularning ZIP arxivlari.

> Eski `build\publish.ps1` va `build\publish.bat` skriptlari **WPF versiyasini**
> (`src/DarsJadvali.UI`) yig'adi — yangi reliz uchun ulardan foydalanmang.

Batafsil: [`docs/CHIQARISH.md`](docs/CHIQARISH.md) va [`build/README.md`](build/README.md).

---

## Ma'lumotlar bazasi qayerda saqlanadi

| OS | Yo'l |
|----|------|
| Windows | `%LOCALAPPDATA%\DarsJadvali\darsjadvali.db` |
| macOS / Linux | `~/.local/share/DarsJadvali/darsjadvali.db` |

Bu **oddiy SQLite fayli**. Zaxira nusxa olish uchun shu faylni ko'chirib qo'yish kifoya
(dastur yopiq holatda). Dasturni o'chirib qayta o'rnatsangiz ham ma'lumot yo'qolmaydi —
bazani tozalash uchun shu faylni o'chirib tashlang.

---

## Migration qo'shish

Ma'lumotlar bazasi sxemasi o'zgarsa (yangi maydon, yangi entity):

```bash
dotnet ef migrations add <Nom> -p src/DarsJadvali.Infrastructure -s src/DarsJadvali.Infrastructure
```

`dotnet ef` o'rnatilmagan bo'lsa:

```bash
dotnet tool install --global dotnet-ef --version 8.*
```

> **Diqqat:** loyiha `net8.0` ga mo'ljallangan, shuning uchun `dotnet-ef` ning **8.x**
> versiyasi kerak. Agar tizimda 9.x yoki 10.x global o'rnatilgan bo'lsa, migratsiya
> yaratishda xato beradi. Bunday holda global versiyani almashtirmasdan, loyihaga
> lokal tool sifatida qo'shish mumkin:
>
> ```bash
> dotnet new tool-manifest
> dotnet tool install dotnet-ef --version 8.*
> dotnet dotnet-ef migrations add <Nom> -p src/DarsJadvali.Infrastructure -s src/DarsJadvali.Infrastructure
> ```

Migratsiya dastur ishga tushganda `IDatabaseInitializer` tomonidan avtomatik qo'llanadi
(`Database.MigrateAsync()`), qo'lda `dotnet ef database update` qilish shart emas.

---

## Testlar

```bash
dotnet test
```

Testlar xotiradagi (in-memory) SQLite bazasida ishlaydi — haqiqiy bazaga tegmaydi.
Qamrov: validatsiyaning 10 ta qoidasi, `IScheduleService`, avtomatik generator va repozitoriylar.

---

## Kelajakda kengaytirish: genetik algoritm qo'shish

Generator interfeys orqali ulanadi, shuning uchun **mavjud kodga tegmasdan** yangi
algoritm qo'shish mumkin. Hozir `GreedyScheduleGenerator` ishlaydi ("Greedy (tezkor)").

### 1-qadam. Yangi sinf yaratish

`src/DarsJadvali.Application/Generation/GeneticScheduleGenerator.cs`:

```csharp
using DarsJadvali.Application.Abstractions;
using DarsJadvali.Application.Validation;

namespace DarsJadvali.Application.Generation;

/// <summary>Genetik algoritm asosidagi jadval generatori.</summary>
public sealed class GeneticScheduleGenerator : IScheduleGenerator
{
    private readonly IUnitOfWork _uow;
    private readonly IScheduleValidator _validator;

    public GeneticScheduleGenerator(IUnitOfWork uow, IScheduleValidator validator)
    {
        _uow = uow;
        _validator = validator;
    }

    public string Name => "Genetik (sifatli)";
    public string Description => "Populyatsiya va mutatsiya asosida optimal jadval izlaydi.";

    public async Task<GenerationResult> GenerateAsync(
        GenerationOptions options,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken ct = default)
    {
        // options.PopulationSize, options.MutationRate, options.MaxIterations,
        // options.RandomSeed — shu yerda ishlatiladi.
        // Har avlodda: progress?.Report(new GenerationProgress(i, options.MaxIterations, fitness, "..."));
        // Yakunda eng yaxshi yechim ScheduleEntry sifatida saqlanadi.
        throw new NotImplementedException();
    }
}
```

### 2-qadam. DI da ro'yxatdan o'tkazish

`src/DarsJadvali.Application/DependencyInjection/ApplicationServiceRegistration.cs`:

```csharp
public static IServiceCollection AddApplication(this IServiceCollection services)
{
    // ... mavjud servislar ...

    services.AddScoped<IScheduleValidator, ScheduleValidator>();

    // Ikkala generator ham ro'yxatda — UI foydalanuvchiga tanlash imkonini beradi:
    services.AddScoped<IScheduleGenerator, GreedyScheduleGenerator>();
    services.AddScoped<IScheduleGenerator, GeneticScheduleGenerator>();

    return services;
}
```

### 3-qadam. UI da tanlash

ViewModel `IEnumerable<IScheduleGenerator>` ni oladi va ro'yxatni `Name` bo'yicha ko'rsatadi:

```csharp
public sealed class ScheduleViewModel
{
    private readonly IReadOnlyList<IScheduleGenerator> _generators;

    public ScheduleViewModel(IEnumerable<IScheduleGenerator> generators)
        => _generators = generators.ToList();

    public IReadOnlyList<IScheduleGenerator> Generators => _generators;   // ComboBox uchun
}
```

> Diqqat: bitta `IScheduleGenerator` talab qiladigan joylar `GetRequiredService<IScheduleGenerator>()`
> chaqirganda **oxirgi** ro'yxatdan o'tgan implementatsiya qaytadi.

Batafsil: [`docs/ARXITEKTURA.md`](docs/ARXITEKTURA.md).

---

## Hujjatlar

- [`docs/FOYDALANISH.md`](docs/FOYDALANISH.md) — foydalanuvchi uchun qadamma-qadam qo'llanma
- [`docs/ARXITEKTURA.md`](docs/ARXITEKTURA.md) — arxitektura, qatlamlar, kengaytirish
- [`docs/CONTRACT.md`](docs/CONTRACT.md) — qatlamlar orasidagi shartnoma (imzolar)
- [`build/README.md`](build/README.md) — yig'ish skriptlari

---

## Muallif va aloqa

**Abduxalil Voxidjonov**

Telegram: **[@abduxalilvoxidjonov](https://t.me/abduxalilvoxidjonov)** — https://t.me/abduxalilvoxidjonov

Savol, taklif yoki xatolik haqida xabar — Telegram orqali yozing.

---

## Loyihani qo'llab-quvvatlash (donat)

Dastur bepul. Agar foydali bo'lsa va rivojlanishiga hissa qo'shmoqchi bo'lsangiz:

| | |
|---|---|
| **Karta turi** | Humo |
| **Karta raqami** | **`9860 3501 4679 1495`** |
| **Karta egasi** | Abduxalil Voxidjonov |

Rahmat! Har bir qo'llab-quvvatlash yangi imkoniyatlar qo'shilishiga yordam beradi.
