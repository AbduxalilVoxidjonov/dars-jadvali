# Dars Jadvali Tuzuvchi

Maktab va o'quv markazlari uchun **dars jadvalini tuzish** dasturi.
**Windows va macOS** uchun ish stoli dasturi (Avalonia), ma'lumotlar bazasi — SQLite.

Jadvalni qo'lda ham, bir tugma bilan avtomatik ham tuzish mumkin.
Avtomatik tuzish `DarsJadvali.Scheduling` yadrosida bajariladi — sof algoritm kutubxonasi,
**bitta ham tashqi paketsiz** (AC-3, ejection chain, simulated annealing + tabu,
Hopcroft–Karp xona taqsimlash). Batafsil: [`docs/ALGORITM.md`](docs/ALGORITM.md).

### Internet kerakmi?

**Ish uchun — yo'q.** Ma'lumot bazasi, jadval tuzish, chop etish — hammasi kompyuterda,
oflayn ishlaydi. Tarmoqqa **bitta** joyda murojaat qilinadi:

> **"Dastur haqida"** sahifasini ochganingizda dastur fon rejimida `github.com` ga
> so'rov yuborib, **yangi versiya chiqqan-chiqmaganini** tekshiradi
> (`AboutViewModel.LoadAsync` → `IUpdateChecker`). Internet bo'lmasa —
> "tekshirib bo'lmadi" deb yozadi, boshqa hech narsa buzilmaydi.

Boshqa hech bir ekran tarmoqqa chiqmaydi. `DarsJadvali.Web` sinov serveri esa faqat
`127.0.0.1` ga bog'lanadi va uning `index.html` sahifasida bitta ham tashqi CDN havolasi yo'q.

---

## Imkoniyatlar

- **O'qituvchilar** — F.I.Sh., telefon, rang (jadvalda ajratib ko'rsatish uchun), faol/nofaol holat
- **Fanlar** — nomi, qisqa kodi, rangi
- **Avtomatik rang tanlash** — yangi fan yoki o'qituvchi qo'shilganda paletkadan hali
  ishlatilmagan rang o'zi tanlanadi (qo'lda o'zgartirish mumkin)
- **Sinflar va guruhlar** — sinf, bo'linish (`ClassDivision`) va guruhlar
  (butun sinf · 1/2 guruh · o'g'il/qiz). Bir fanni ikki guruhga yoki turli fanlarni
  ikki guruhga bir vaqtda qo'yish qo'llab-quvvatlanadi
- **Ikki smena** — smena bo'yicha filtr; dars soatlari smenalar bo'ylab uzluksiz raqamlanadi
- **Chorak** — har bir chorak **alohida jadval varianti**, oldingisidan nusxa olish mumkin
- **Biriktirmalar** — kim, qaysi fandan, qaysi sinfda, haftasiga necha soat
- **Hafta kunlari va dars soatlari** — qaysi kunlar ish kuni, har bir dars raqamining aniq vaqti
- **O'qituvchi vaqti** (`TimeOff`) — ruxsat / tavsiya etilmaydi / taqiq darajalari
- **Jadval taxtasi** — virtualizatsiyalangan to'r, **kartani "qo'lga olib" ko'chirish**
  (3 rangli baholash, SHIFT — mumkin joylarni yoritish, CTRL — guruh bilan ko'chirish),
  **bekor qilish/qaytarish 100 qadam**, qulflash, zoom 50–200%,
  joylashtirilmagan darslar paneli
- **Avtomatik generatsiya** — `DarsJadvali.Scheduling` yadrosi; seed va qidiruv byudjeti
  sozlanadi, jarayon ko'rinib turadi va istalgan paytda bekor qilinadi
  (bekor qilinsa eski jadval joyida qoladi)
- **Chop etish** — JSON dizaynlarga asoslangan dvigatel, **4 ta dizayn**, PDF va HTML
- **Localhost sinov rejimi** — `127.0.0.1` da, API-kalit va rate-limit bilan; brauzerda,
  istalgan operatsion tizimda sinab ko'rish

### Tekshiriladigan qoidalar

Ikkita tekshiruv qatlami bor.

**1. Avtomatik generatsiya yadrosi** (`DarsJadvali.Scheduling`) — hard qoidalar hech qachon
buzilmaydi (`C-GBL-01/02/03/06/07/08`, `C-AVL-01..05`, `C-ROM-01/02`, `C-DBL-01`), soft
qoidalar esa jarima bilan optimallashtiriladi (sinf oynalari, fan bir kunda bir marta,
haftalik tekis taqsimot, o'qituvchi oynalari va yuklamasi, ...). To'liq ro'yxat va
og'irliklar: [`docs/ALGORITM.md`](docs/ALGORITM.md).

**2. Qo'lda joylashtirish validatori** (`ScheduleValidator`, eski `ScheduleEntry` yo'li) —
10 ta qoida:

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

Bundan tashqari `GROUP_DIVISION_OVERLAP` — turli bo'linishdagi guruhlar bir slotda
(masalan "1-guruh" + "o'g'illar"). Buni bazaning o'zi ushlay olmaydi, shuning uchun u
Application qatlamida tekshiriladi (`GroupDivisionOverlapValidator`).

Bandlikning o'zi esa **bazaning unikal indeksi** bilan ham to'siladi:
`UX_CardOccurrences_Schedule_Resource_Slot` — **guruh aniqligida**.

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
│   │   ├── Entities/                #   sxema v2: Term, Shift, Period, Grade, SchoolClass,
│   │   │                            #   ClassDivision, StudentGroup, Classroom, Lesson(+join),
│   │   │                            #   Card(+CardClassroom), CardOccurrence, TimeOff
│   │   │                            #   + eski (v1): ClassGroup, TeacherAssignment,
│   │   │                            #     TeacherAvailability, LessonSlot, ScheduleEntry
│   │   └── Enums/                   #   WeekDay, ResourceKind, AvailabilityLevel, ...
│   │
│   ├── DarsJadvali.Scheduling/      # net8.0 — SOF ALGORITM YADROSI, 0 ta tashqi paket
│   │   ├── Model/                   #   TimeGrid, SlotMask (512 bit), Card, Problem, Solution
│   │   ├── Constraints/             #   HardRules, ConstraintSet (og'irliklar)
│   │   ├── Pipeline/                #   Verifier, Propagator (AC-3), Constructor,
│   │   │                            #   EjectionChainRepair, Optimizer (SA+tabu), Relaxer
│   │   ├── Rooms/                   #   RoomAssigner + HopcroftKarp
│   │   └── Util/                    #   Xoshiro256SS (determinizm)
│   │
│   ├── DarsJadvali.Application/     # net8.0 — biznes-mantiq (EF Core'ni bilmaydi)
│   │   ├── Abstractions/            #   IRepository<T>, IUnitOfWork, ITransactionalUnitOfWork,
│   │   │                            #   ISchedulingStore, ICardOccurrenceProjector, IUpdateChecker
│   │   ├── Board/                   #   ICardBoardService, CardView, UnplacedLessonView
│   │   ├── Scheduling/              #   ISchedulingMapper, ScheduleGenerationService,
│   │   │                            #   SchedulingIdMap, GroupDivisionOverlapValidator
│   │   ├── Validation/              #   IScheduleValidator, Conflict, ValidationResult
│   │   ├── Generation/              #   [Obsolete] GreedyScheduleGenerator (eski yo'l)
│   │   ├── Export/                  #   PDF/HTML uchun ma'lumot modeli
│   │   ├── Services/                #   ITeacherService, IScheduleService, IScheduleSetService, ...
│   │   └── DependencyInjection/     #   AddApplication()
│   │
│   ├── DarsJadvali.Infrastructure/  # net8.0 — EF Core + SQLite + PDFsharp
│   │   ├── Persistence/             #   AppDbContext, UnitOfWork, DatabaseInitializer
│   │   │   ├── Configurations/      #   indekslar, OnDelete, HasQueryFilter
│   │   │   ├── Backfill/            #   LegacyToV2Backfill, ClassStructureFactory
│   │   │   ├── Projection/          #   CardOccurrenceProjector
│   │   │   ├── DatabaseBackupService.cs      #   VACUUM INTO → backups/
│   │   │   └── SqliteExceptionTranslator.cs  #   tipli istisnolar
│   │   ├── Migrations/              #   EF Core migratsiyalari (V2_01 … V2_07)
│   │   ├── Export/Printing/         #   JSON dizaynlar + PrintDesignPdfRenderer (PDFsharp)
│   │   ├── Update/                  #   GitHubUpdateChecker
│   │   └── DependencyInjection/     #   AddInfrastructure(), SqlitePragmaInterceptor (WAL)
│   │
│   ├── DarsJadvali.Desktop/         # net8.0 — ASOSIY dastur: Avalonia + Material.Avalonia + MVVM
│   │   ├── Views/                   #   .axaml sahifalar (Windows va macOS uchun bitta kod)
│   │   ├── ViewModels/              #   CommunityToolkit.Mvvm + ColorPalette
│   │   ├── Services/Timetable/      #   DragSession, CommandHistory (undo), TimetableBoard
│   │   ├── Converters/, Styles/, Models/
│   │   ├── ViewLocator.cs           #   ViewModel → View moslashuvi
│   │   └── App.axaml.cs             #   DI (Microsoft.Extensions.Hosting) + baza init
│   │
│   ├── DarsJadvali.UI/              # ESKI WPF versiyasi — .sln DAN CHIQARILGAN,
│   │   │                            #   faqat diskda tarixiy nusxa sifatida turibdi
│   │   └── ...
│   │
│   └── DarsJadvali.Web/             # net8.0 — localhost sinov serveri (minimal API + wwwroot)
│       ├── Endpoints/               #   /api/board/... + eski /api/schedule (Obsolete)
│       ├── Security/                #   API-kalit, rate-limit, 127.0.0.1
│       └── Dtos/
│
├── tests/
│   ├── DarsJadvali.Tests/           # net8.0 — xunit (Domain/Application/Infrastructure)
│   └── DarsJadvali.Scheduling.Tests/# net8.0 — xunit (yadro: determinizm, hard qoidalar,
│                                    #   xona matching, bekor qilish, benchmark)
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
    ├── CONTRACT.md                  # Qatlamlar orasidagi shartnoma — v2 (joriy)
    ├── CONTRACT-v1.md               # Eski shartnoma (WPF + ScheduleEntry davri) — arxiv
    ├── ARXITEKTURA.md               # Arxitektura va kengaytirish nuqtalari
    ├── ALGORITM.md                  # Generatsiya yadrosi: fazalar, cheklovlar, determinizm
    ├── MIGRATSIYA.md                # Eski bazadan sxema v2 ga o'tish
    ├── FOYDALANISH.md               # Foydalanuvchi uchun qadamma-qadam qo'llanma
    ├── CHIQARISH.md                 # Reliz chiqarish (Windows va macOS)
    ├── AVALONIA-KOCHIRISH.md        # WPF'dan Avalonia'ga o'tish tarixi
    └── research/                    # Tadqiqot va loyihalash hujjatlari (arxiv)
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
.\build\publish-windows.ps1                        # x64 va x86, self-contained (standart)
.\build\publish-windows.ps1 -Runtime win-x86       # faqat 32-bitli Windows
.\build\publish-windows.ps1 -FrameworkDependent    # kichik hajm, .NET 8 Runtime alohida kerak
```

> Skriptda **`-SelfContained` parametri yo'q** (u ataylab olib tashlangan — PowerShell
> `-SelfContained $false` ni ham `$true` deb qabul qilardi). Kichik hajmli versiya uchun
> `-FrameworkDependent` **bayrog'ini** ishlating.

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
| macOS | `~/Library/Application Support/DarsJadvali/darsjadvali.db` |
| Linux | `~/.local/share/DarsJadvali/darsjadvali.db` |

Bu **oddiy SQLite fayli**. Zaxira nusxa olish uchun shu faylni ko'chirib qo'yish kifoya
(dastur yopiq holatda). Dasturni o'chirib qayta o'rnatsangiz ham ma'lumot yo'qolmaydi —
bazani tozalash uchun shu faylni o'chirib tashlang.

Baza **WAL rejimida** ishlaydi (`SqlitePragmaInterceptor`: `journal_mode=WAL`,
`busy_timeout=5000`, `foreign_keys=ON`) — shuning uchun ishlash paytida yonida
`darsjadvali.db-wal` va `darsjadvali.db-shm` fayllari turadi. **Nusxa olishdan oldin
dasturni yoping.**

Sxema yangilanishidan oldin dastur avtomatik zaxira oladi (`VACUUM INTO`):
`<baza papkasi>/backups/darsjadvali-YYYYMMDD-HHMMSS.db`, oxirgi **10 tasi** saqlanadi.
Batafsil: [`docs/MIGRATSIYA.md`](docs/MIGRATSIYA.md).

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

Ikkita test loyihasi bor:

| Loyiha | Qamrov |
|---|---|
| `tests/DarsJadvali.Tests` | Validatsiyaning 10 ta qoidasi, `IScheduleService`, repozitoriylar, migratsiyalar va backfill. Xotiradagi (in-memory) SQLite bazasida — haqiqiy bazaga tegmaydi |
| `tests/DarsJadvali.Scheduling.Tests` | Yadro: `SlotMask`, hard qoidalar, delta izchilligi, bo'linish teglari, xona matching, determinizm, bekor qilish, benchmark |

To'liq benchmark o'lchovi (uzoq davom etadi):

```bash
DJ_BENCH=1 dotnet test --filter Category=Benchmark
```

---

## Kengaytirish nuqtalari

Avtomatik tuzish endi **`DarsJadvali.Scheduling`** yadrosida bajariladi. Eski
`IScheduleGenerator` / `GreedyScheduleGenerator` yo'li hamon kompilyatsiya qilinadi,
lekin `[Obsolete]` bilan belgilangan — yangi ish uchun ishlatilmaydi.

| Kerak bo'lsa | Nimani o'zgartirish |
|---|---|
| Yangi soft qoida (jarima bilan) | `Scheduling/Constraints/` da `ConstraintBase` dan meros olib yangi sinf yozing va uni `ConstraintSet.CreateDefault()` ga qo'shing |
| Og'irliklarni sozlash | `ConstraintSet.CreateDefault()` dagi `Weight` qiymatlari — [`docs/ALGORITM.md`](docs/ALGORITM.md) §3.2 |
| Yangi hard qoida | `Scheduling/Constraints/HardRules.cs` + `SolutionState.TryApply` invariantlari |
| Yadro ↔ baza bog'lanishi | `Application/Scheduling/SchedulingMapper.cs` — EF entity'lari va yadro indekslari orasidagi **yagona** ko'prik |
| Boshqa ma'lumotlar bazasi | `IRepository<T>`, `IUnitOfWork`, `ISchedulingStore`, `ICardOccurrenceProjector` |
| Yangi chop etish shakli | `Infrastructure/Export/Printing/Designs/` ga yangi JSON dizayn qo'shing — kod o'zgartirilmaydi |
| Boshqa interfeys (web, mobil) | `ICardBoardService` va `IScheduleGenerationService` ni chaqiring |

Yadro **hech qanday tashqi paketga bog'liq emas va EF Core'ni ko'rmaydi** — uni alohida
sinash va almashtirish shuning uchun oson. Batafsil: [`docs/ARXITEKTURA.md`](docs/ARXITEKTURA.md).

---

## Hujjatlar

- [`docs/FOYDALANISH.md`](docs/FOYDALANISH.md) — foydalanuvchi uchun qadamma-qadam qo'llanma
- [`docs/ARXITEKTURA.md`](docs/ARXITEKTURA.md) — arxitektura, qatlamlar, kengaytirish
- [`docs/ALGORITM.md`](docs/ALGORITM.md) — generatsiya yadrosi: fazalar, cheklovlar, determinizm
- [`docs/CONTRACT.md`](docs/CONTRACT.md) — qatlamlar orasidagi shartnoma (imzolar), **v2**
- [`docs/MIGRATSIYA.md`](docs/MIGRATSIYA.md) — eski bazadan sxema v2 ga o'tish
- [`docs/CHIQARISH.md`](docs/CHIQARISH.md) — reliz chiqarish (Windows va macOS)
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
