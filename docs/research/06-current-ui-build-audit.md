# 06 — Prezentatsiya qatlami va eksport/update infratuzilmasi auditi

> **Qamrov:** `src/DarsJadvali.Desktop`, `src/DarsJadvali.UI`, `src/DarsJadvali.Web`,
> `src/DarsJadvali.Infrastructure/Export`, `src/DarsJadvali.Infrastructure/Update`,
> `README.md`, `docs/*.md`.
> **Qamrovdan tashqari:** Domain, Application, Persistence/Migrations (boshqa auditlarda).
> **Sana:** 2026-08-14 · **Tekshirilgan commit:** `3c18261`
> **Turi:** faqat o'qish (audit) — hech qanday fayl o'zgartirilmagan.

---

## 1. Loyihalar xaritasi

### 1.1 `.sln` tarkibi

`DarsJadvali.sln` da **6 ta manba loyiha + 1 test loyihasi** bor, hammasi `Debug|Release` da
quriladi (`Build.0` yozuvlari bilan):

| Loyiha | TFM | Turi | Holati |
|---|---|---|---|
| `DarsJadvali.Domain` | net8.0 | lib | faol (audit qamrovidan tashqari) |
| `DarsJadvali.Application` | net8.0 | lib | faol (audit qamrovidan tashqari) |
| `DarsJadvali.Infrastructure` | net8.0 | lib | faol |
| **`DarsJadvali.Desktop`** | **net8.0** | **WinExe (Avalonia 11.2.3)** | **✅ ASOSIY, FAOL** |
| **`DarsJadvali.UI`** | **net8.0-windows** | **WinExe (WPF)** | **❌ O'LIK KOD, lekin quriladi** |
| `DarsJadvali.Web` | net8.0 | ASP.NET Core | yordamchi/demo (5-bo'limga qarang) |
| `DarsJadvali.Tests` | net8.0 | xunit | Domain/Application/Infrastructure ga bog'langan |

### 1.2 Desktop vs UI dublikati — ANIQ JAVOB

**`DarsJadvali.Desktop` — faol. `DarsJadvali.UI` — o'lik kod (eski WPF), lekin `.sln` dan
olib tashlanmagan va har bir `dotnet build` da qayta quriladi.**

Dalillar:

1. **Ikkalasi ham `.sln` da va ikkalasi ham muvaffaqiyatli quriladi.** Audit paytida
   `dotnet build DarsJadvali.sln` ishga tushirildi — natija:
   ```
   DarsJadvali.UI      -> src/DarsJadvali.UI/bin/Debug/net8.0-windows/DarsJadvali.dll
   DarsJadvali.Desktop -> src/DarsJadvali.Desktop/bin/Debug/net8.0/DarsJadvali.dll
   Build succeeded. 0 Warning(s), 0 Error(s)
   ```
   Ya'ni bu shunchaki "unutilgan papka" emas — u kompilyatsiya qilinadi, CI vaqtini yeydi
   va refaktoringda "ikkinchi marta tuzatish kerak bo'lgan" kod bo'lib qoladi.

2. **Ikkala loyihaning `AssemblyName` i bir xil: `DarsJadvali`**
   (`DarsJadvali.Desktop.csproj:8`, `DarsJadvali.UI.csproj:7`). Ikkalasi ham
   `DarsJadvali.dll` / `DarsJadvali.exe` chiqaradi. Bu chalkashlikning eng xavfli manbasi:
   noto'g'ri `bin` papkasidan yig'ilgan `DarsJadvali.exe` eski WPF dasturi bo'lib chiqadi.

3. **`UI` funksional jihatdan orqada qolgan** — bu uning o'likligini isbotlaydi:
   - `DarsJadvali.UI/ViewModels/` da **`AcademicYearsViewModel` YO'Q**. Ya'ni WPF versiyasi
     o'quv yillari va bir nechta jadval variantlari (`Schedule` / `ScheduleSet`) ni
     umuman bilmaydi. Desktop da bu bor (`AcademicYearsViewModel.cs`, 685 qator).
   - `UI/ViewModels/DashboardViewModel.cs` konstruktorida `ISchoolTimetablePdfExporter`
     yo'q → **WPF versiyasida PDF eksport yo'q**.
   - `UI/ViewModels/TimetableViewModel.cs` — 541 qator, Desktop dagi 733 qatorli
     variantning eski nusxasi (PDF eksport, `MainViewModel.PendingClassGroupId`
     integratsiyasi yo'q).
   - `UI/App.xaml.cs:37-49` da ro'yxatdan o'tgan ViewModel'lar 9 ta,
     `Desktop/App.axaml.cs:61-70` da 10 ta.

4. **Yig'ish skriptlari ham shuni tasdiqlaydi:**
   - `build/publish-macos.sh:54`, `build/publish-windows.ps1:56` → `src/DarsJadvali.Desktop`
   - `build/publish.ps1:6` va `build/publish.bat:5` → o'z izohlarida
     "DIQQAT: bu skript **ESKI WPF** loyihasini yig'adi" deb yozilgan, lekin skriptlar
     hamon repozitoriyda turibdi.
   - `README.md:107` — "`DarsJadvali.UI/` — net8.0-windows — **ESKI WPF versiyasi**".

5. **⚠️ Xavfli tafsilot:** `UI/App.xaml.cs:30` da
   `services.AddInfrastructureSqlite(InfrastructureServiceRegistration.DefaultDbPath)` —
   ya'ni eski WPF dasturi **Desktop bilan bir xil SQLite faylini** ochadi. Agar
   foydalanuvchi eski `DarsJadvali.exe` ni ishga tushirsa, u yangi sxemadagi bazani
   eski (o'quv yilini bilmaydigan) mantiq bilan ochadi.

**Tavsiya (1-darajali):** `DarsJadvali.UI` ni `.sln` dan chiqarib, `git rm -r` bilan
o'chirish (tarix `git log` da qoladi). Bilan birga `build/publish.ps1` va
`build/publish.bat` ni ham. Agar hozircha o'chirishga tayyor bo'lmasangiz — hech
bo'lmaganda `.sln` dan chiqarib, `AssemblyName` ni `DarsJadvali.Legacy` ga o'zgartiring.

### 1.3 Testlar

`tests/DarsJadvali.Tests/DarsJadvali.Tests.csproj` faqat Domain / Application /
Infrastructure ga havola qiladi. **Prezentatsiya qatlami uchun 0 ta test bor** —
na ViewModel testlari, na Avalonia.Headless UI testlari.

---

## 2. Ekranlar ro'yxati (Desktop — faol loyiha)

| # | View (`Views/*.axaml`) | Code-behind | ViewModel | Vazifasi | Holati |
|---|---|---|---|---|---|
| 1 | `MainWindow.axaml` (144) | 22 | `MainViewModel` (324) | Qobiq: chap menyu (10 band), yuqorida o'quv yili + jadval tanlagichi, pastda status, `IsBusy` progressbari | ✅ ishlaydi |
| 2 | `DashboardView.axaml` (226) | **319 ⚠️** | `DashboardViewModel` (637) | KPI kartalari, avtomatik tuzish + progress + bekor qilish, tekshiruv (konflikt ro'yxati), butun maktab jadvali (faqat ko'rish), sinf filtri, PDF | ⚠️ to'r kodda quriladi |
| 3 | `TimetableView.axaml` (261) | 31 | `TimetableViewModel` (733) | Tahrirlanadigan jadval to'ri; sinf/o'qituvchi rejimi; o'ng panelda "dars qo'yish" formasi; konfliktlar; PDF; jadvalni tozalash | ⚠️ drag-drop yo'q |
| 4 | `TeachersView.axaml` (144) | 12 | `TeachersViewModel` (256) | O'qituvchilar CRUD + rang tanlash (`ColorPalette.cs`) | ✅ |
| 5 | `SubjectsView.axaml` (136) | 12 | `SubjectsViewModel` (296) | Fanlar CRUD | ✅ |
| 6 | `ClassGroupsView.axaml` (112) | 12 | `ClassGroupsViewModel` (267) | Sinflar CRUD (+ asosiy xona) | ✅ |
| 7 | `AssignmentsView.axaml` (198) | 12 | `AssignmentsViewModel` (428) | Biriktirmalar: o'qituvchi × sinf × fan × haftalik soat | ✅ |
| 8 | `WorkDaysView.axaml` (137) | 12 | `WorkDaysViewModel` (322) | Ish kunlari + dars soatlari jadvali (`LessonSlot`) | ✅ |
| 9 | `AvailabilityView.axaml` (175) | 12 | `AvailabilityViewModel` (374) | O'qituvchi bandligi to'ri (kun × dars soati), "Hammasi"/"Hech biri" | ✅ |
| 10 | `AcademicYearsView.axaml` (235) | 12 | `AcademicYearsViewModel` (**685 ⚠️**) | O'quv yillari CRUD + har yil ichida jadval variantlari (qo'shish, nusxalash, faol qilish, o'chirish) | ⚠️ juda katta |
| 11 | `AboutView.axaml` (192) | 12 | `AboutViewModel` (199) | Dastur haqida, GitHub yangilanish tekshiruvi, Telegram, xayriya kartasi | ✅ |
| 12 | `DialogWindow.axaml` (75) | 37 | `Models/DialogModel.cs` | Umumiy modal muloqot oynasi (Info/Error/Confirm/Validation) | ✅ |

**Yordamchi ViewModel'lar** (alohida fayllarda emas — bu topishni qiyinlashtiradi):

| Tur | Qayerda yashaydi | Muammo |
|---|---|---|
| `TimetableCellViewModel` | `ViewModels/TimetableViewModel.cs:648` | Fayl nomi bilan mos emas |
| `ConflictRowViewModel`, `ScheduleColors`, `SchoolTimetableSnapshot`, `ClassTimetableRowViewModel`, `DashboardCellViewModel`, `ClassFilterOption` | `ViewModels/ClassTimetableViewModel.cs` (193 qator, 6 ta ochiq tur) | Bitta faylda 6 ta public tur |
| `AssignmentRowViewModel` | `ViewModels/AssignmentsViewModel.cs:395` | — |
| `WorkDayRowViewModel`, `LessonSlotRowViewModel` | `ViewModels/WorkDaysViewModel.cs:259,293` | — |
| `LessonColumnViewModel`, `TeacherDayRowViewModel`, `LessonCellViewModel` | `ViewModels/AvailabilityViewModel.cs:263,282,352` | — |
| `ScheduleRowViewModel` | `ViewModels/AcademicYearsViewModel.cs:647` | — |
| `UniqueViolation` (infratuzilma yordamchisi) | `ViewModels/SubjectsViewModel.cs:279` | ViewModel faylida EF/SQLite xato matnini tahlil qilish |

### 2.1 aSc TimeTables ga nisbatan YETISHMAYDIGAN ekranlar

| aSc imkoniyati | Loyihada |
|---|---|
| Xona (kabinet) ko'rinishi va xona jadvali | ❌ yo'q (`RoomNumber` — shunchaki matn maydoni) |
| Talaba / guruh (seminar group) ko'rinishi | ❌ yo'q |
| O'rinbosarlik (substitution) moduli | ❌ yo'q |
| Card lock / qulflangan dars | ❌ yo'q |
| Undo / Redo | ❌ yo'q |
| Chop etish dizaynlari muharriri | ❌ yo'q (bitta qat'iy PDF shabloni) |
| Bir vaqtning o'zida bir nechta ko'rinish (split view) | ❌ yo'q |
| Fan/o'qituvchi bo'yicha "yuk" (workload) diagrammasi | ❌ yo'q (faqat sonli KPI) |

---

## 3. MVVM va arxitektura muammolari

Format: `fayl:qator — muammo — qanday sindiradi — tuzatish`

### 🔴 Kritik

**M-01. Bitta `DbContext` ustida parallel amallar — dastur ishdan chiqadi**

`ViewModels/TimetableViewModel.cs:214, 225, 236, 247` — to'rtta `partial void On…Changed`
metodi `_ = RefreshGridAsync();` deb **fire-and-forget** chaqiradi. Bir xil DI qamrovidagi
(`NavigationService.cs:34`) barcha `Application` servislari `Scoped`
(`ApplicationServiceRegistration.cs:17-33`) va bitta `AppDbContext` ni bo'lishadi
(`InfrastructureServiceRegistration.cs:39`, `AddDbContext` = Scoped).

*Qanday sindiradi:* foydalanuvchi "O'qituvchi bo'yicha" rejimiga o'tganda
`OnIsTeacherModeChanged` → `_ = RefreshGridAsync()` ishga tushadi; shu payt ComboBox
ko'rinishi o'zgargani uchun `OnFilterTeacherChanged` ham ishlaydi →
ikkinchi `_ = RefreshGridAsync()`. Ikkita `async` DB o'qish bitta `DbContext` ustida
kesishadi → `InvalidOperationException: A second operation was started on this context
instance before a previous operation completed`. Bu `App.axaml.cs:142`
`OnUnhandledException` da ushlanadi va foydalanuvchi tushunarsiz xato oynasini ko'radi.

*Xuddi shu naqsh:* `AssignmentsViewModel.cs:148`, `AvailabilityViewModel.cs:106`,
`AcademicYearsViewModel.cs:126`, `MainViewModel.cs:110, 192, 202, 212`.

*Tuzatish:* (a) har bir `RefreshGridAsync` ni `SemaphoreSlim(1,1)` yoki
`CancellationTokenSource` bilan seriyalash; (b) har bir amal uchun alohida qamrov
(`IServiceScopeFactory`) ochish — `MainViewModel.cs:129` da allaqachon shunday qilingan,
uni sahifa ViewModel'lariga ham tarqatish; (c) uzoq muddatda —
`IDbContextFactory<AppDbContext>` ga o'tish.

**M-02. Buyruqlarda `CanExecute` yo'q — ikki marta bosish = ikkita parallel yozuv**

`TimetableViewModel.cs:423` `PlaceAsync`, `:539` `DeleteSelectedAsync`, `:551`
`ClearScheduleAsync`, `DashboardViewModel.cs:141` `RefreshAsync`, `:482` `ValidateAllAsync`,
`:529` `ClearScheduleAsync`, `:566` `ExportPdfAsync` — barchasi `[RelayCommand]`, hech
birida `CanExecute = nameof(...)` yo'q. Faqat `GenerateAsync` (`:380`) himoyalangan.

*Qanday sindiradi:* "Qo'yish" tugmasini ikki marta tez bosish → ikkita `PlaceAsync`
parallel → M-01 dagi `DbContext` xatosi yoki ikkita bir xil dars.

*Tuzatish:* `ViewModelBase` ga `IsBusy` asosidagi umumiy `CanExecute` mexanizmi qo'shish
(`[RelayCommand(CanExecute = nameof(IsNotBusy))]` + `IsBusy` setterda
`NotifyCanExecuteChanged`).

**M-03. `CancellationToken` hech qachon uzatilmaydi**

`MainViewModel.cs:304` — `await viewModel.LoadAsync()` (token yo'q).
`ViewModelBase.cs:17` `LoadAsync(CancellationToken ct = default)` — barcha sahifalarda
`ct` bor, lekin navigatsiya doim `default` beradi.

*Qanday sindiradi:* katta bazada "Bosh sahifa" yuklanayotganda foydalanuvchi boshqa
sahifaga o'tsa, eski `LoadAsync` bekor qilinmaydi — u tugagach allaqachon `Dispose`
qilingan qamrovdagi `DbContext` ga murojaat qiladi
(`NavigationService.cs:53` `previousScope?.Dispose()`) → `ObjectDisposedException`.

*Tuzatish:* `MainViewModel` da har navigatsiyada yangi `CancellationTokenSource`,
eskisini `Cancel()`; `NavigateAsync` da tokenni `LoadAsync` ga uzatish.

### 🟠 Jiddiy

**M-04. `DashboardView.axaml.cs` — 319 qator kod bilan UI qurish**

`Views/DashboardView.axaml.cs:58-150` `BuildTimetable()` butun maktab jadvalini
imperativ ravishda `Grid` ga chizadi: `Border`, `TextBlock`, `StackPanel` obyektlari
qo'lda yaratiladi, ustun kengliklari `:17-19` da **qat'iy pikselda**
(`ClassColumnWidth = 136`, `LessonColumnWidth = 104`, `DayColumnWidth = 150`).
Fayl izohida sabab yozilgan: Avalonia'da `SharedSizeGroup` yo'q.

Taqqoslash uchun: eski WPF versiyasi shu ekranni **to'liq deklarativ XAML** da
qilgan (`DarsJadvali.UI/Views/DashboardView.xaml:61, 111, 357, 375` —
`SharedSizeGroup="TimetableClassColumn"` / `"TimetableLessonColumn"`). Ya'ni bu
Avalonia'ga ko'chirishda yuz bergan **regressiya**.

*Qanday sindiradi:* (a) uzun sinf nomi yoki uzun kun nomi 136/150 px ga sig'maydi —
matn kesiladi; (b) hech qanday virtualizatsiya yo'q: 40 sinf × 8 dars × 6 kun ≈
**2000+ `Border` + 3000+ `TextBlock`** bir vaqtning o'zida vizual daraxtda →
sahifa ochilishi soniyalar davom etadi; (c) `Timetable` xossasi har o'zgarganda
(`:49-55`) butun daraxt qaytadan quriladi — sinf filtrini almashtirish ham to'liq
qayta qurish.

*Tuzatish:* `ItemsRepeater` (Avalonia 11) yoki virtualizatsiyalangan `ItemsControl`
+ `DataTemplate`; ustun kengligini `Grid` + `x:Name` va `Bind` orqali
sinxronlash yoki maxsus `Panel` (o'lchov mantiqi bitta joyda).

**M-05. `Cells` to'liq qayta quriladi (`Clear()` + N ta `Add`)**

`TimetableViewModel.cs:296-349` `BuildGrid()` — `Cells.Clear()` dan keyin
`(kunlar+1) × (darslar+1)` ta yangi `TimetableCellViewModel` yaratadi va
`ObservableCollection` ga bittalab qo'shadi. Har `Add` bitta `CollectionChanged`
hodisasi tug'diradi.

*Qanday sindiradi:* 6 kun × 7 dars = 56 katak → 56 ta layout passi; katta maktabda
(10 dars, 7 kun) 80 ta. Bitta darsni qo'yganda ham (`PlaceAsync:471`
`RefreshGridAsync()`) butun to'r qayta quriladi, tanlov yo'qoladi
(`BuildGrid:294` `SelectedCell = null`), aylantirish holati tiklanmaydi.

*Tuzatish:* to'rni bir marta qurish (kunlar/darslar o'zgarganda), keyin faqat
o'zgargan katakning xossalarini yangilash (`EntryId`, `SubjectName`, ...).

**M-06. ViewModel'lar Avalonia UI turlariga bog'langan (`IBrush`, `Thickness`, `Color`)**

`ViewModels/TimetableViewModel.cs:714` `public IBrush Background`, `:719` `BorderBrush`,
`:722` `public Thickness BorderThickness`;
`ViewModels/ClassTimetableViewModel.cs:100, 129, 132, 136-193` — butun `ScheduleColors`
sinfi `Avalonia.Media` ga tayanadi;
`ViewModels/ColorPalette.cs` (94 qator) ham shunday.

*Qanday sindiradi:* (a) ViewModel'lar UI-freymvorksiz test qilinmaydi — `IBrush`
yaratish uchun Avalonia yuklanishi kerak; (b) qora mavzu (dark theme) qo'shib
bo'lmaydi — ranglar ViewModel ichida qotib qolgan, `DynamicResource` bilan
almashmaydi; (c) `Converters/` papkasida 6 ta konverter bor
(`ColorCodeToBrushConverter`, `ConflictSeverityToBrushConverter`, ...) va ular
`AppStyles.axaml:21-26` da ro'yxatdan o'tgan — lekin `TimetableView.axaml` va
`DashboardView` ular o'rniga ViewModel'dagi `IBrush` ni ishlatadi → **ikki xil
yondashuv yonma-yon**.

*Tuzatish:* ViewModel faqat `string ColorCode` / `bool IsSelected` / `ConflictSeverity`
qaytarsin; rangga aylantirish `IValueConverter` yoki XAML `Style` selektorlari
(`Border.cell:selected`) orqali bo'lsin.

**M-07. ViewModel'lar bir-biriga to'g'ridan-to'g'ri bog'langan (`MainViewModel` in'ektsiyasi)**

`TimetableViewModel.cs:86` `MainViewModel main`, `DashboardViewModel.cs:110`,
`AcademicYearsViewModel.cs:71` — uchta sahifa `MainViewModel` (singleton) ni to'g'ridan
qabul qiladi. Aloqa `MainViewModel.PendingClassGroupId` (`:90`) — **o'zgaruvchan
umumiy holat** orqali: Dashboard yozadi (`:377` `GoToTimetable(item.ClassGroupId)`),
Timetable o'qiydi va tozalaydi (`:163-175`).

*Qanday sindiradi:* navigatsiya parametri "global o'zgaruvchi" bo'lib qolgan; agar
`LoadAsync` xato bersa `PendingClassGroupId` tozalanmaydi va keyingi safar noto'g'ri
sinf ochiladi. Test qilish uchun butun `MainViewModel` grafini qurish kerak.

*Tuzatish:* `INavigationService.NavigateTo<TVm>(object? parameter)` — parametrni
navigatsiya xizmati uzatsin; ViewModel'lar bir-birini bilmasin.

**M-08. Versiya raqami ikki joyda**

`Directory.Build.props:11` `<Version>1.0.0</Version>` va
`src/DarsJadvali.Domain/Common/AppInfo.cs:10` `public const string Version = "1.0.0"`.

*Qanday sindiradi:* reliz chiqarganda bittasini unutish oson; `AboutViewModel` va
yangilanish tekshiruvi (`AppInfo.HttpUserAgent`, `AppInfo.cs:58`) `AppInfo.Version` ga
tayanadi, o'rnatuvchi esa `Directory.Build.props` ga → GitHub'da yangi versiya
bo'lsa ham dastur "yangi versiya bor" demasligi mumkin.

*Tuzatish:* `AppInfo.Version` ni `Assembly.GetEntryAssembly()!.GetName().Version`
yoki `AssemblyInformationalVersionAttribute` dan olish; `Directory.Build.props`
yagona manba bo'lsin.

### 🟡 O'rta

**M-09. `App.axaml.cs:162` — `GetAwaiter().GetResult()` UI oqimida**
`_host?.StopAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult()` — dastur yopilayotganda
UI oqimini 3 soniyagacha bloklaydi. Deadlock ehtimoli past (Avalonia'da
`SynchronizationContext` `Post` bilan ishlaydi), lekin "dastur osilib qoldi"
taassuroti beradi. → `desktop.Exit` o'rniga `ShutdownRequested` da `async` yopish.

**M-10. `App.axaml.cs:44` — `_ = StartAsync(desktop)` fire-and-forget**
Ichida `try/catch` bor, shuning uchun xavfsiz; lekin `OnFrameworkInitializationCompleted`
qaytgach oyna hali yo'q — bu oraliqda kelgan istisnolar
`Dispatcher.UIThread.UnhandledException` ga tushadi va `DialogService.GetOwner()`
(`DialogService.cs:191-211`) `null` qaytaradi → egasiz oyna ochiladi. Ishlaydi, lekin
"ko'rinmas oyna" holati mumkin.

**M-11. `AboutViewModel.cs:98` — `_ = CheckUpdateCommand.ExecuteAsync(null)`**
`LoadAsync` sinxron qaytadi, tarmoq so'rovi fon rejimida ketadi. Bu **to'g'ri
yondashuv**, lekin `CancellationToken` uzatilmaydi (`:121` `_updateChecker.CheckAsync()`)
— foydalanuvchi sahifadan chiqib ketsa ham so'rov davom etadi va tugagach
allaqachon kerak bo'lmagan ViewModel'ning xossalarini o'zgartiradi.

**M-12. `DashboardView.axaml.cs:43` — `PropertyChanged` obunasi bekor qilinmaydi**
Obuna faqat `DataContext` o'zgarganda uziladi (`:36`), `DetachedFromVisualTree` da emas.
Sahifadan chiqilganda ViewModel (transient) View'ga havolani ushlab turadi.
Amalda ikkalasi ham birga chiqindiga ketadi, lekin naqsh noto'g'ri.

**M-13. `ViewLocator.cs:29` — `fullName.Replace("ViewModel", "View")`**
Agar kelajakda `ViewModelsHelperViewModel` kabi nom paydo bo'lsa yoki namespace ichida
"ViewModel" so'zi uchrasa, noto'g'ri tur qidiriladi. `Type.GetType` faqat joriy
assembly'da izlaydi. Xato holatda ekran o'rniga matn ko'rsatiladi (`:47`) — bu yaxshi,
lekin xato loglanmaydi.

**M-14. `MenuItemModel.IconKind` ishlatilmaydi**
`Models/MenuItemModel.cs:5` — "hozircha chizilmaydi, kelajak uchun".
`MainViewModel.cs:53-62` da 10 ta Material Design ikona nomi yozilgan
(`"ViewDashboard"`, `"AccountTie"`, ...), lekin `MainWindow.axaml:42-49` da
`ItemTemplate` faqat `Title` ni ko'rsatadi. `Material.Avalonia` paketi ulangan
(`csproj:22`), lekin ikonalar ishlatilmayapti — menyu quruq matn ro'yxati.

**M-15. `StaticResource` va `DynamicResource` aralash**
`MainWindow.axaml:15, 20, 76-78` — `StaticResource AppBackgroundBrush`;
`TimetableView.axaml:10-11, 49`, `DashboardView.axaml:207` — `DynamicResource`.
Bir xil kalitlar (`AppStyles.axaml:8-18`). Mavzu almashtirish qo'shilganda
`StaticResource` ishlatilgan joylar yangilanmaydi.

**M-16. `AppStyles.axaml` da uslublar ikki marta yozilgan**
`:30-48` `ControlTheme` sifatida va `:53-71` `Style Selector` sifatida — bir xil
qiymatlar (`FontSize 22`, `#212121`, ...). Bittasini o'zgartirib ikkinchisini
unutish oson. Amalda faqat `Classes="AppPageTitle"` ishlatiladi.

**M-17. `RequestedThemeVariant="Light"` qotib qolgan**
`App.axaml:6` va `MaterialTheme BaseTheme="Light"` (`:14`) — qora mavzu imkoniyati yo'q.
Ranglar bundan tashqari `ScheduleColors` (ViewModel) va `DashboardView.axaml.cs:80`
(`#F6F4FB`) da ham qattiq yozilgan.

**M-18. `ViewModelBase` juda yupqa (18 qator)**
`ViewModelBase.cs` da faqat `IsBusy`, `StatusMessage`, `LoadAsync`. Natijada har bir
ViewModel'da bir xil naqsh takrorlanadi: `try { IsBusy = true; ... } catch (Exception ex)
{ await _dialogs.ErrorAsync(...); } finally { IsBusy = false; }` — bu blok
**Desktop bo'ylab 30+ marta** uchraydi (masalan `TimetableViewModel.cs:116-197,
254-290, 402-420, 460-523, 574-595, 600-640`).

*Tuzatish:* `ViewModelBase.RunAsync(Func<CancellationToken,Task>, string errorMessage)`
yordamchi metodi — `IsBusy`, `try/catch`, `OperationCanceledException`, dialog —
hammasi bir joyda.

**M-19. `UniqueViolation` — ViewModel qatlamida SQLite xato matnini tahlil qilish**
`ViewModels/SubjectsViewModel.cs:279` — baza xato xabarini matn bo'yicha tekshirib,
"unikal indeks buzildi" degan xulosa chiqaradi. Bu Infrastructure qatlamining ishi;
SQLite versiyasi yoki EF Core yangilanganda xabar matni o'zgarsa — jimgina buziladi.

**M-20. `Progress<GenerationProgress>` UI oqimini bosadi**
`DashboardViewModel.cs:406-413` — `Progress<T>` UI oqimida yaratilgan, shuning uchun
har bir hisobot `Dispatcher` ga `Post` qilinadi. Generator har bir dars uchun hisobot
bersa (1000+ dars), UI oqimiga 1000+ `Post` tushadi → progressbar "sakraydi", oyna
javob bermay qoladi. → hisobotlarni vaqt bo'yicha cheklash (throttle, masalan 100 ms).

### ✅ Yaxshi tomonlar (buzmaslik kerak)

- `CommunityToolkit.Mvvm` 8.3.2 to'g'ri ishlatilgan: `[ObservableProperty]`,
  `[RelayCommand]`, `[NotifyPropertyChangedFor]` (`TimetableViewModel.cs:652-675`).
- **ViewModel'da `DbContext` YO'Q** — grep bo'yicha `Desktop` va `UI` da
  `DbContext`/`EntityFrameworkCore` ga birorta ham murojaat yo'q (faqat izohda).
  Hammasi `Application` interfeyslari orqali. Bu juda yaxshi.
- **`.Result` / `.Wait()` yo'q** — yagona blokirovka `App.axaml.cs:162` (M-09).
- `AvaloniaUseCompiledBindingsByDefault=true` (`csproj:9`) + har bir View'da
  `x:DataType` — binding xatolari kompilyatsiya vaqtida tutiladi.
- `NavigationService` har sahifaga alohida DI qamrovi beradi va eskisini
  `Dispose` qiladi (`NavigationService.cs:34-54`) — `DbContext` "eskirib qolish"
  muammosining oldini oladi.
- `DialogService.RunOnUiAsync` (`:214-235`) — ViewModel fon oqimidan chaqirsa ham
  dialog UI oqimida ochiladi.
- `Program.cs:38-56` — `DARSJADVALI_AUTOCLOSE` avtomatik sinov uchun; `App.axaml.cs:21`
  `DARSJADVALI_DB` — testlar uchun alohida baza. O'ylangan.

---

## 4. Jadval ko'rinishi (timetable grid) tahlili

### 4.1 Hozirgi holat — ikkita mustaqil to'r amalga oshirilgan

| | **Bosh sahifa** (butun maktab) | **Dars jadvali** (tahrirlash) |
|---|---|---|
| Fayl | `DashboardView.axaml.cs:58` `BuildTimetable()` | `TimetableView.axaml:157-168` |
| Texnika | Kodda qurilgan `Grid` (`x:Name="TimetableBodyGrid"`) | `ItemsControl` + `UniformGrid` `ItemsPanel` |
| Ma'lumot | `SchoolTimetableSnapshot` → `Blocks` → `Rows` → `Cells` | Yassi `ObservableCollection<TimetableCellViewModel> Cells` |
| Ustun kengligi | Qat'iy piksel (136/104/150) | `UniformGrid Columns="{Binding GridColumnCount}"` — teng bo'linadi |
| Virtualizatsiya | ❌ yo'q | ❌ yo'q (`UniformGrid` virtualizatsiyalamaydi) |
| Aylantirish | Ikki qavat `ScrollViewer` (`DashboardView.axaml:210-220`) | Bitta `ScrollViewer` |
| Tahrirlash | ❌ faqat ko'rish | ✅ tanlash + o'ng panel |

Bu **kod dublikati**: bir xil ma'lumot (sinf × kun × dars) ikki xil model va ikki xil
render bilan ikki marta yozilgan.

### 4.2 Interaktivlik

| Imkoniyat | Holat | Dalil |
|---|---|---|
| Katakni tanlash | ✅ | `TimetableView.axaml:27` `PointerPressed="OnCellPointerPressed"` → `TimetableView.axaml.cs:17-30` → `SelectCommand` |
| O'ng tugma menyusi ("O'chirish") | ✅ | `TimetableView.axaml:29-35` `ContextFlyout` |
| Dars qo'yish | ⚠️ faqat o'ng paneldagi 4 ta ComboBox orqali (`:192-241`) | `PlaceCommand` |
| **Drag & drop** | ❌ **YO'Q** | Butun `Desktop` va `UI` bo'ylab `DragDrop`, `AllowDrop`, `DoDragDrop`, `PointerMoved` — **0 ta natija** |
| Mumkin bo'lgan pozitsiyalarni yoritish | ❌ yo'q | Hech qanday "candidate slot" mantiq yo'q |
| Jonli to'qnashuv ko'rsatish | ❌ yo'q — to'qnashuv faqat "Qo'yish" bosilgandan **keyin** ro'yxatda chiqadi (`TimetableViewModel.cs:465` `ShowConflicts`) | To'rda konflikt belgisi yo'q |
| Card lock (qulflash) | ❌ yo'q | — |
| Undo / Redo | ❌ yo'q | — |
| Bir nechta katakni tanlash | ❌ yo'q | `SelectedCell` — bitta (`:48`) |
| Klaviatura bilan yurish | ❌ yo'q | `KeyDown` ishlovchisi yo'q |
| Katakni ko'chirish (boshqa vaqtga) | ❌ yo'q — o'chirib, qaytadan qo'yish kerak | — |
| Xona ko'rsatish | ⚠️ matn sifatida (`RoomDisplayText`) | Xona konflikti tekshiriladi, lekin xona ko'rinishi yo'q |

### 4.3 Ishlash tezligi muammolari

1. **Har bir amaldan keyin to'liq qayta qurish.** `PlaceAsync:471`, `DeleteEntryAsync:419`,
   `ClearScheduleAsync:594`, `OnFilterClassGroupChanged:236` — hammasi
   `RefreshGridAsync()` → yangi DB so'rovi + `BuildGrid` + `Cells.Clear()` + N ta `Add`.
   Bitta darsni qo'yish uchun 56-80 ta ViewModel obyekti qaytadan yaratiladi.
2. **Tanlov va aylantirish holati yo'qoladi** (`BuildGrid:294-296`). Foydalanuvchi
   ketma-ket 10 ta dars qo'ymoqchi bo'lsa, har safar katakni qaytadan topib bosishi kerak.
3. **Bosh sahifada virtualizatsiya yo'q.** 40 sinf × 8 dars × 6 kun ≈ 2000 `Border`.
   Har bir `Border` ichida `StackPanel` + 2-3 `TextBlock` → ~8000 vizual element.
   Avalonia bularning hammasini o'lchaydi va joylashtiradi.
4. **`UniformGrid` ichidagi `Columns="{Binding GridColumnCount}"` mo'rt.**
   `ItemsPanelTemplate` ning `DataContext` i `ItemsControl` dan meros olinadi; bu
   Avalonia'da ishlaydi, lekin `ItemsPanel` almashtirilganda binding qayta
   baholanmasligi mumkin — kun soni o'zgarganda (ish kunlari sozlamasi) to'r
   noto'g'ri chiziladi degan xavf bor.
5. **`ScheduleColors.Light()` da `lock`** (`ClassTimetableViewModel.cs:168`) — UI
   oqimida har katak uchun qulf olinadi. 2000 katakda 2000 ta `lock`. Kichik, lekin
   keraksiz.

### 4.4 aSc TimeTables talabi bilan farq

| aSc | Loyiha | Farq darajasi |
|---|---|---|
| Kartani sichqoncha bilan sudrab ko'chirish | Yo'q (ComboBox forma) | 🔴 tubdan |
| Sudrash paytida yashil/qizil "mumkin/mumkin emas" yoritish | Yo'q | 🔴 tubdan |
| Real vaqtda to'qnashuv sababini ko'rsatish | Faqat qo'ygandan keyin ro'yxat | 🔴 |
| Sinf / o'qituvchi / xona / talaba ko'rinishlari | Sinf va o'qituvchi bor; xona va talaba yo'q | 🟠 |
| Kartani qulflash (lock) | Yo'q | 🟠 |
| Undo/Redo | Yo'q | 🟠 |
| Bir nechta kartani tanlash va guruh bilan ishlash | Yo'q | 🟡 |
| Katak ustida sichqoncha turganda batafsil ma'lumot (tooltip) | Yo'q | 🟡 |
| Jadvalni zoom qilish | Yo'q | 🟡 |

**Xulosa:** hozirgi jadval ko'rinishi — "ma'lumotlar jadvali + forma". aSc darajasiga
chiqish uchun uni **butunlay qayta yozish** kerak (8-bo'limga qarang), chunki
hozirgi yassi `ObservableCollection<Cell>` + `UniformGrid` modeli sudrash,
qulflash va ko'p tanlovni ko'tarmaydi.

---

## 5. Web loyihasi holati

### 5.1 Umumiy xulosa

`DarsJadvali.Web` — **mahsulot emas, "localhost sinov qobig'i"**. Buni kodning o'zi
tan oladi: `Program.cs:8-13` va `wwwroot/index.html:221` ("localhost test rejimi"),
`docs/CONTRACT.md:425`. U Desktop bilan bir xil `Application`/`Infrastructure`
qatlamlariga yupqa qobiq.

**Git tarixi:** `git log -- src/DarsJadvali.Web/` — atigi **bitta commit**
(`a9c7ed6`, birinchi versiya). Keyingi ikki commit Web'ga tegmagan. **Yig'ilmaydi ham:**
`build/publish-*.ps1|sh` fayllarining hech birida `DarsJadvali.Web` yo'q — faqat
`build/run-web.sh|ps1` uni dev rejimida ishga tushiradi.

### 5.2 Endpointlar

**51 ta endpoint**, hammasi `/api` ostida, `Program.cs:94-100` da ulanadi:

| Fayl | Endpointlar |
|---|---|
| `CatalogEndpoints.cs` (153) | `teachers`, `subjects`, `classgroups` — har biri GET/GET{id}/POST/PUT/DELETE (15 ta) |
| `AssignmentEndpoints.cs` (78) | `assignments` CRUD + `{id}/hours` (6 ta) |
| `SettingsEndpoints.cs` (167) | `workdays` GET/PUT, `lessonslots` GET/PUT, `availability/{id}` GET/PUT (**eski**), `availability/{id}/lessons` GET/PUT (**joriy**) (8 ta) |
| `ScheduleEndpoints.cs` (130) | `schedule` GET, `place`, `move`, DELETE{id}, DELETE, `generate`, `validate`, `pdf` (8 ta) |
| `ScheduleSetEndpoints.cs` (246) | `academicyears` CRUD, `schedules` CRUD + `active`, `duplicate`, `activate` (13 ta) |
| `AboutEndpoints.cs` (23) | `GET /api/about` — `AppInfo` konstantalari + **bazaning absolyut yo'li** (`:21`) |

**DTO'lar:** `Dtos/Dtos.cs` da 26 ta `sealed record` (20 javob + 5 so'rov + 1).
Xaritalash — qo'lda yozilgan kengaytma metodlari (`Dtos/Mapper.cs`, AutoMapper yo'q).
`TimeSpan` ⇄ `"HH:mm"` (`Mapper.cs:15-27`).

### 5.3 Muhim topilmalar

**W-01 🔴 Autentifikatsiya UMUMAN YO'Q.**
`Program.cs` da `AddAuthentication`, `[Authorize]`, `RequireAuthorization()` — hech biri
yo'q. **51 ta endpointning hammasi anonim**, jumladan barcha `DELETE` lar
(`DELETE /api/schedule` butun maktab jadvalini o'chiradi). CORS sozlanmagan, HTTPS yo'q
(`Program.cs:30` — `http://localhost:5080` qotib qolgan), anti-forgery yo'q,
rate limiting yo'q. `AllowedHosts: "*"` (`appsettings.json:9`).

Standart holatda faqat `localhost` ga bog'lanadi (`Program.cs:30-32`) — bu yagona
himoya. Lekin `--urls`/`ASPNETCORE_URLS` qabul qilinadi (`:27-28`), ya'ni
`http://0.0.0.0:5080` bersangiz — **butun tarmoq uchun ochiq, shifrlanmagan, to'liq
CRUD API**. Hech qayerda bu haqda ogohlantirish yo'q.

**W-02 🔴 Desktop bilan BIR XIL SQLite faylini yozadi, xavfsizlik choralarisiz.**
`Program.cs:18-22` → `InfrastructureServiceRegistration.DefaultDbPath` —
`Desktop/App.axaml.cs:82` va `UI/App.xaml.cs:30` bilan aynan bir xil.
Butun repo bo'ylab `journal_mode`, `WAL`, `busy_timeout`, `Pooling` — **0 ta natija**;
ulanish satri yalang'och `Data Source={path}` (`InfrastructureServiceRegistration.cs:82`).
Natija: Desktop yozayotgan paytda Web yozsa → `SQLITE_BUSY` / "database is locked" →
`Program.cs:88` orqali umumiy 500. Qayta urinish yo'q.

**W-03 🔴 `IsActive` — global o'zgaruvchan holat.**
`POST /api/schedules/{id}/activate` (`ScheduleSetEndpoints.cs:168`) **butun bazaga
tegishli** bayroqni almashtiradi. Web API esa faqat faol jadvalga yoza oladi (W-04),
shuning uchun SPA'dagi oddiy ko'rinadigan jadval tanlagichi (`index.html:623, 629-649`)
**barcha mijozlar va ishlab turgan Desktop dasturi uchun holatni o'zgartiradi**.
Ikki brauzer varag'i (yoki brauzer + Desktop) qaysi jadval "joriy" ekani ustida
jimgina kurashadi.

**W-04 🟠 `ScheduleId` DTO'da yo'q — Web faqat faol jadval bilan ishlaydi.**
`Dtos.cs:151-158` `ScheduleDraftRequest` da `ScheduleId` maydoni yo'q,
`Mapper.cs:156-157` `ToDraft` 8 ta argumentdan 7 tasini uzatadi → `ScheduleId = null`.
Ya'ni Web'dan aniq bir jadval variantiga yozib bo'lmaydi.

**W-05 🟠 To'liq SPA — Desktop'ning ikkinchi nusxasi.**
`wwwroot/index.html` — 1659 qator: 207 qator CSS (`:7-214`), 9 ta sahifa uchun
markup (`:254-408`), ~1240 qator **toza vanilla JS** (`:417-1657`). Freymvork yo'q,
CDN yo'q, build qadami yo'q, tashqi so'rov yo'q. Hash-routing (`go()` :544).

Sahifalari Desktop ekranlari bilan **deyarli bir xil**: Bosh sahifa, O'qituvchilar,
Fanlar, Sinflar, Biriktirmalar, Hafta kunlari, O'qituvchi vaqti, Dars jadvali,
Dastur haqida + jadval variantlari modali (`openSetManager` :654-783).
Ya'ni **butun UI uchinchi marta yozilgan** (Desktop + WPF + SPA).

**W-06 🟡 SPA'da ham drag-drop yo'q, lekin konflikt ko'rsatish Desktop'dan YAXSHIROQ.**
`dragstart`/`dragover`/`drop` — 0 ta natija. Dars qo'yish: bo'sh katakdagi `＋` →
modal forma (`drawGrid` :1500, `placeForm` :1519-1556).
Lekin `submitPlacement` (`:1559-1589`) konfliktlarni rangli kartalarda ko'rsatadi
(`conflictsHtml` :522-530) va `Error` darajali konflikt bo'lmasagina
"⚠️ Baribir qo'yish" tugmasini chiqaradi (`:1571, 1579`) — Desktop'dagi modal
dialogdan (`DialogService.ConfirmWarningsAsync`) ko'ra qulayroq UX.
**Bu naqshni Desktop'ga ko'chirish kerak.**

**W-07 🟡 51 endpointdan 11 tasi (22%) SPA tomonidan hech qachon chaqirilmaydi.**
Jumladan `POST /api/schedule/move` (`ScheduleEndpoints.cs:36`) — **sudrab ko'chirish
uchun yozilgan, lekin sudrash UI'si hech qachon qurilmagan**. Bu Application
qatlamida `MoveAsync` allaqachon borligini bildiradi — drag-drop uchun backend tayyor.
Shuningdek eski `availability/{id}` juftligi (`SettingsEndpoints.cs:74, 85`) —
o'z izohida "yangi interfeys `/lessons` dan foydalanadi" deb yozilgan.

**W-08 🟡 Xatoliklar `ProblemDetails` emas.**
Yagona global `try/catch` middleware (`Program.cs:51-78`) — stack trace sizdirmaydi,
5xx/4xx ni to'g'ri loglaydi, `Response.HasStarted` ni hisobga oladi. Bu yaxshi.
Lekin shakl `{ "error": "..." }`, RFC 7807 emas; `AddProblemDetails()` yo'q.
Topilmagan marshrutlar bo'sh 404 qaytaradi.

Status kodlari nuqsonlari: POST create'lar **201 emas, 200** qaytaradi va `Location`
sarlavhasi yo'q (`CatalogEndpoints.cs:39, 85, 131`); PUT `null` tanali `200 OK`
qaytarishi mumkin (`:50, 96, 142`); `DELETE /api/schedule/{id}` mavjud bo'lmagan id
uchun ham 204 (`ScheduleEndpoints.cs:42-46`); `null` JSON tanasi uchta PUT'da
`NullReferenceException` → 500 (`SettingsEndpoints.cs:25-27, 46-48, 86`) —
to'rtinchisida (`:136-137`) tuzatilgan, qolganlariga qo'llanmagan.

**W-09 🟡 `GET /api/about` shaxsiy ma'lumot va server yo'lini oshkor qiladi.**
`AboutEndpoints.cs:21` — bazaning absolyut yo'li
(`/Users/<ism>/.local/share/DarsJadvali/darsjadvali.db`);
`AppInfo.cs:22` — Humo karta raqami. Ikkalasi ham autentifikatsiyasiz endpointda.

**W-10 🟡 Versiya SPA'da uchinchi marta qotib qolgan.**
`index.html:234` — `v1.0.0` qo'lda yozilgan (`/api/about` dan o'qilmaydi).
`AppInfo.cs:10` va `Directory.Build.props:11` bilan birga — **versiya 3 joyda**.

**Ijobiy:** TODO/FIXME/HACK — 0 ta; izohga olingan o'lik kod — 0 ta; XSS uchun
`esc()` yordamchisi (`index.html:438`) izchil ishlatilgan; chop etish uchun
alohida CSS (`:190-213`).

### 5.4 Qaror

Web loyihasi **hozircha saqlansin, lekin `.sln` da "namuna" sifatida belgilansin va
hech qachon tarqatilmasin**. Uning ikkita real qiymati bor:
1. `POST /api/schedule/move` — drag-drop uchun Application qatlami tayyorligini
   isbotlaydi.
2. SPA'dagi konflikt ko'rsatish naqshi (W-06) — Desktop UX uchun namuna.

Agar kelajakda Web haqiqiy mahsulotga aylantirilsa: autentifikatsiya, WAL rejimi,
optimistik konkurentlik (`RowVersion`) va `IsActive` global bayrog'idan voz kechish
**majburiy** shart.

---

## 6. Export/Update infratuzilmasi

### 6.1 PDF eksport — `SchoolTimetablePdfExporter.cs` (658 qator)

**Arxitektura — yaxshi.** Uch bosqichli quvur: o'lchash (`BuildRenderBlocks` :105) →
sahifalarga bo'lish (`Paginate` :109) → chizish (:112-132). Sahifalash chizishdan
oldin bo'lgani uchun pastdagi `n / N` raqami to'g'ri chiqadi. Qatlamlash ham toza:
faylda `DbContext`, repozitoriy yoki EF turi **umuman yo'q** — faqat
`ITimetableExportModelBuilder` (:17-23).

**E-01 🔴 O'qituvchi rejimida PDF NOTO'G'RI ma'lumot beradi.**
`Desktop/ViewModels/TimetableViewModel.cs:608`:
```csharp
ClassGroupId = IsClassMode ? FilterClassGroup?.Id : null,
```
Foydalanuvchi bitta o'qituvchining jadvalini ko'rib turib "PDF yuklab olish" bosadi —
`ClassGroupId = null` bo'ladi va **butun maktab jadvali** eksport qilinadi.
Hech qanday ogohlantirish yo'q. *Tuzatish:* eksporterga `TeacherId` variantini
qo'shish yoki hech bo'lmaganda foydalanuvchini ogohlantirish
(Web SPA `index.html:1594-1602` da aynan shu holatda toast ko'rsatadi — Desktop'da
u ham yo'q).

**E-02 🟠 `SuggestFileName` `options` ni e'tiborsiz qoldiradi.**
`SchoolTimetablePdfExporter.cs:55-59` — parametr qabul qilinadi va tashlab yuboriladi.
Bitta sinf eksporti ham, butun maktab eksporti ham bir xil
`Maktab-jadvali-2026-08-14.pdf` nomini taklif qiladi → foydalanuvchi bilmasdan
oldingi faylni ustiga yozadi.

**E-03 🟠 Matn jimgina kesiladi va ustunlar ustma-ust tushadi.**
`Wrap` (:461-521) so'z bo'yicha, keyin harf bo'yicha bo'ladi va `…` qo'yadi — bu yaxshi.
Lekin `:517` da qator limiti tugagan bo'lsa **qolgan matn ellipsissiz tashlanadi** —
o'quvchi matn kesilganini bilmaydi. Bundan yomoni: **sinf nomi (`:358`) va kun
sarlavhalari (`:275`) umuman o'lchanmaydi va o'ralmaydi** — uzun sinf nomi 54 pt
ustundan chiqib, qo'shni ustun ustiga chiziladi.

**E-04 🟠 Sahifadan baland qator sahifadan tashqariga chiziladi.**
`:587-599` — izohda ochiq yozilgan: `taken = 1; // bitta qator sahifadan baland —
baribir chizamiz`. Uzun fan + o'qituvchi nomi bilan bitta katakda 5 qatorgacha matn
bo'lishi mumkin (2+2+1, `:441, :446, :453`) → qator pastki chegaradan va
kolontituldan chiqib ketadi.

**E-05 🟠 Butun tartib ~40 ta qotib qolgan raqam; shablon/uslub obyekti yo'q.**
Chekkalar `:28-31`, sarlavha balandligi `:33`, ustun kengliklari `:40-41`,
ranglar `:264, :229, :300, :373`, 11 ta shrift o'lchami `:629-639`.
`PdfExportOptions` da yagona tartib parametri — `Landscape` (`PdfExportOptions.cs:13`).
A4 o'lchami **ikki joyda** yozilgan: `PageSize.A4` (:168) va
`595.28 / 841.89` (:176-177) — qog'oz o'lchamini o'zgartirish uchun ikkalasini
tuzatish kerak, aks holda o'lchash va chizish mos kelmaydi.

**E-06 🔴 Eksport variantlari — aSc'ga nisbatan katta bo'shliq.**

| aSc chop etish imkoniyati | Loyihada |
|---|---|
| Butun maktab | ✅ (`ClassGroupId == null`) |
| Bitta sinf | ✅ (`ClassGroupId = id`) — lekin bu ham **xuddi shu stacked-grid**, alohida bir sahifalik sinf varag'i emas |
| **O'qituvchi jadvali** | ❌ **yo'q** (UI'da o'qituvchi rejimi bor — E-01) |
| Xona (kabinet) jadvali | ❌ yo'q |
| Talaba / guruh jadvali | ❌ yo'q |
| Kun bo'yicha (butun maktab, bir kun) | ❌ yo'q |
| O'rinbosarlik varaqasi | ❌ yo'q |
| HTML eksport | ❌ yo'q |
| Excel / CSV eksport | ❌ yo'q |
| Bir nechta chop etish dizayni / shablon | ❌ yo'q |
| Printerga to'g'ridan-to'g'ri chop etish | ❌ yo'q (faqat `byte[]`) |

**E-07 🟡 `PdfExportOptions.ScheduleId` erishib bo'lmaydigan kod.**
`PdfExportOptions.cs:22` — `TimetableExportModelBuilder.cs:43` uni hisobga oladi,
lekin **hech bir chaqiruvchi uni to'ldirmaydi**: `DashboardViewModel.cs:575-579`,
`TimetableViewModel.cs:606-610`, `ScheduleEndpoints.cs:91-98`. Ya'ni faol bo'lmagan
jadval variantini eksport qilib bo'lmaydi.

**E-08 🟠 PDF UI oqimida yaratilishi mumkin.**
`Render` — to'liq sinxron CPU ishi (`:65`), `await ...ConfigureAwait(false)` (`:48`)
dan keyin chaqiriladi. SQLite/EF chaqiruvi ko'pincha sinxron yakunlanadi, shunda
`Render` **chaqiruvchi oqimda — Avalonia UI oqimida** ishlaydi
(`DashboardViewModel.cs:580`, `TimetableViewModel.cs:611`) → katta eksportda oyna
muzlaydi. Yo'lda hech qayerda `Task.Run` yo'q.
Asosiy xarajat — `MeasureString` chiziqli sikllarda (`:480, :499-512, :531`);
40 sinf × 8 dars × 6 kun × 3 maydon ≈ minglab o'lchash chaqiruvi.

**E-09 🟡 `TimetableExportModelBuilder` ikkita qatlamda ro'yxatdan o'tgan.**
`ApplicationServiceRegistration.cs:33` (`AddScoped`, shartsiz) va
`InfrastructureServiceRegistration.cs:94` (`TryAddScoped`). Hozircha ikkalasi bir xil
turga hal bo'ladi, lekin ro'yxatdan o'tkazish tartibi natijani belgilaydi — mo'rt.

**E-10 🟡 Lokalizatsiya infratuzilmasi yo'q.**
`.resx` ham, `IStringLocalizer` ham yo'q. Faqat eksport yo'lida 12+ qotib qolgan
o'zbekcha satr: `"Dars jadvali"` (:77, 201, 206), `"SINF"` (:268), `"SOAT"` (:270),
`" (dav.)"` (:357), `"xona: "` (:452), `"Maktab-jadvali-"` (:58),
`"Hali dars qo'yilmagan"` (`TimetableExportModel.cs:48`),
`"{n}-soat"` (`TimetableExportModelBuilder.cs:108`).
Sana esa to'g'ri — `CultureInfo.InvariantCulture` bilan qotirilgan (:58, 210, 235).

**E-11 🟡 Shrift litsenziyasi — kichik nomuvofiqlik.**
`EmbeddedFontResolver.cs` DejaVu Sans Condensed'ni assembly resursidan beradi —
yondashuv to'g'ri va o'zbek lotin harflari (`oʻ` U+02BB, `ʼ` U+02BC) uchun zarur.
Lekin DejaVu litsenziyasi (`LICENSE-DejaVu.txt:20-21`) litsenziya matnini
"barcha nusxalarda" saqlashni talab qiladi, `.csproj` esa uni
`<None Include=... />` sifatida **`CopyToOutputDirectory` siz** qo'shgan — ya'ni
litsenziya fayli publish natijasiga tushmaydi. Bir qatorlik tuzatish.
Yana: `ResolveTypeface` (`:53-59`) `familyName` va `isItalic` ni e'tiborsiz
qoldiradi va resolver **global** o'rnatiladi (`GlobalFontSettings.FontResolver` :46) —
jarayondagi har qanday PDFsharp shrift so'rovi jimgina DejaVu'ga aylanadi.

### 6.2 Yangilanish tekshiruvi — `GitHubUpdateChecker.cs` (528 qator)

**Bu komponent — loyihaning eng sifatli qismlaridan biri.** Diqqat bilan
o'ylangan, mudofaaviy yozilgan va to'liq oflayn testlar bilan qoplangan
(`tests/DarsJadvali.Tests/UpdateCheckerTests.cs`, ~610 qator).

**Sirlar / tokenlar: YO'Q.** `authorization|bearer|ghp_|github_pat|access_token` —
`src/` va `build/` bo'ylab 0 ta natija. Barcha so'rovlar anonim.
Barcha URL'lar **HTTPS** (`AppInfo.cs:34, 44, 52`).

**Aqlli yechim:** asosiy usul — `HEAD https://github.com/.../releases/latest`,
`AllowAutoRedirect = false` (`:73-74, :162-168`), teg `Location` sarlavhasidan
olinadi (`ExtractTag` :229-259). Bu **API emas**, shuning uchun soatiga 60 ta
so'rov cheklovi qo'llanmaydi. GitHub API faqat reliz izohini olish va zaxira
sifatida ishlatiladi (`:266-276`).

**Xatoliklarni qayta ishlash — a'lo.** `IUpdateChecker.cs:38-43` dagi
"foydalanuvchi bekor qilishidan boshqa hech qachon istisno tashlamaydi" shartnomasi
haqiqatan bajarilgan. Timeout va foydalanuvchi bekor qilishi
`when (userToken.IsCancellationRequested)` filtri bilan **to'g'ri ajratilgan**
(`:185-193`) — ko'pchilik implementatsiyalar shu yerda xato qiladi.
Rate limit (403/429) alohida aniqlanadi (`IsRateLimited` :487-496).
JSON parse xatolari qamrab olingan (`:381-383`).

**Versiya taqqoslash — asosan to'g'ri.** `System.Version` bilan **raqamli**
taqqoslash (`:418`), ya'ni `1.10.0 > 1.9.0` — klassik leksikografik xato yo'q.
`v` prefiksi olib tashlanadi (`:451-452`), `1.2` == `1.2.0` (`:470-474`).

**Yuklab olmaydi / o'rnatmaydi** — faqat xabar beradi;
`AboutViewModel.cs:162-164` reliz sahifasini brauzerda ochadi. **To'g'ri va
xavfsiz qaror, saqlab qolish kerak.**

**`HttpClient` — singleton** (`InfrastructureServiceRegistration.cs:65-66`),
har chaqiruvda yaratilmaydi → soket tugashi yo'q.
**Bloklovchi chaqiruvlar yo'q:** `.Result`, `.Wait()`, `GetAwaiter().GetResult()`,
`Task.Run`, `async void` — faylda 0 ta. Hamma joyda `ConfigureAwait(false)`.

Kamchiliklari:

**U-01 🟠 Tarmoqdan kelgan URL tekshirilmasdan `Process.Start` ga uzatiladi.**
`GitHubUpdateChecker.cs:183` (`Location` sarlavhasi) va `:378`
(`html_url` JSON maydoni) → `UpdateCheckResult.ReleaseUrl` →
`AboutViewModel.cs:124` → `:164` → `OpenUrlAsync` →
`AboutViewModel.cs:171-174` `Process.Start(new ProcessStartInfo(url)
{ UseShellExecute = true })`.
`UseShellExecute = true` bilan satr OS qobig'iga beriladi — `https` bo'lmagan sxema
uni qayta ishlaydigan dasturga yo'naltiriladi. Ekspluatatsiya uchun TLS MITM kerak,
lekin maqsadli muhit (maktab tarmoqlari, filtrlovchi proksi) buni ehtimoldan
xoli qilmaydi. *Tuzatish (bir qator):* `location.Scheme == "https"` va host
`github.com` ekanini tekshirish, aks holda `AppInfo.ReleasesUrl` ga qaytish.

**U-02 🟠 Versiya ikki (aslida uch) joyda qotib qolgan — M-08 bilan bir xil muammo.**
`AppInfo.cs:10` `public const string Version = "1.0.0"` taqqoslash asosi
(`GitHubUpdateChecker.cs:61`), `Directory.Build.props:11` esa o'rnatuvchi versiyasi,
`index.html:234` — SPA'dagi uchinchisi. Ular ajralgan kunda dastur yo har safar
mavjud yangilanish haqida bezovta qiladi, yo real yangilanishni ko'rsatmay qo'yadi —
va **checker'da hech qanday loglash yo'q** (`ILogger` yo'q), shuning uchun buni
hech kim sezmaydi. `const` bo'lgani uchun qiymat barcha bog'liq assembly'larga
kompilyatsiya vaqtida "quyiladi" — qisman qayta qurishda eski qiymat qolib ketishi
mumkin. → assembly versiyasidan olish + `static readonly`.

**U-03 🟡 `HttpResponseMessage` sizib chiqadi.**
`GitHubUpdateChecker.cs:318-336` — `response` `try` dan tashqarida e'lon qilingan
(`:318`), `:325` da tayinlanadi; agar `:326` `ReadAsStringAsync` istisno tashlasa,
`catch` (`:332-336`) `using (response)` blokiga (`:338`) **kirmasdan** qaytadi →
javob va ulanish hech qachon `Dispose` qilinmaydi.

**U-04 🟡 Prerelease taqqoslash noto'g'ri.**
`:455-457` — `-` yoki `+` dan keyingi qism **tashlab yuboriladi, tartiblanmaydi**.
`v2.0.0-beta.1` → `2.0.0` sifatida taqqoslanadi, ya'ni o'rnatilgan `1.0.0` ga
"beta" reliz **barqaror yangilanish sifatida** taklif qilinishi mumkin. Amalda
GitHub'ning `/releases/latest` prerelease'larni chiqarmaydi — ya'ni himoya
kodda emas, GitHub xatti-harakatida.
Bundan tashqari ko'rsatiladigan satr suffiksni saqlaydi (`Normalize` :480-484),
taqqoslash esa tashlaydi — xabar "2.0.0-beta.1" deydi, taqqoslangani "2.0.0".

**U-05 🟡 `CancellationToken` uzatilmaydi.**
`AboutViewModel.cs:92` `LoadAsync(CancellationToken ct)` tokenni oladi, lekin
`:121` `_updateChecker.CheckAsync()` ni **tokensiz** chaqiradi. Sahifadan chiqib
ketilsa ham so'rov davom etadi va tugagach kerak bo'lmagan ViewModel xossalarini
o'zgartiradi (M-11 bilan bir xil).

**U-06 🟡 `ILogger` yo'q.** Dala sharoitida doimiy nosozlikni tashxislab bo'lmaydi.
`AboutViewModel.cs:138-142` `NoRelease` va `Failed` ni bitta kichik izohga
yig'adi — foydalanuvchi uchun to'g'ri, lekin diagnostika uchun hech narsa qolmaydi.

---

## 7. Hujjatlar mosligi

### 7.1 Umumiy diagnoz

Hujjatlar **`a9c7ed6` commitidagi holatni** tasvirlaydi. Undan keyin kodga ikkita
katta to'lqin qo'shilgan va **ularning hech biri hujjatlarga tushmagan**:

1. **O'quv yillari + bir nechta jadval varianti** (`AcademicYear`, `Schedule`,
   `AcademicYearsView`, `IScheduleSetService`)
2. **GitHub yangilanish tekshiruvi** (`IUpdateChecker`, `GitHubUpdateChecker`)

Buni fayl vaqtlari ham tasdiqlaydi: `CONTRACT.md`, `AVALONIA-KOCHIRISH.md`,
`CHIQARISH.md` — 13-avgust 20:17–20:29; `Schedule.cs` va
`AcademicYearsViewModel.cs` — 14-avgust 08:36–08:56.

### 7.2 🔴 Eng jiddiy nomuvofiqliklar

**D-01. `CONTRACT.md` da `DarsJadvali.Desktop` UMUMAN YO'Q.**
`CONTRACT.md:9-15` loyihalar ro'yxatida 6 ta loyiha sanaladi va **asosiy, sotiladigan
dastur ro'yxatda yo'q**. `CONTRACT.md:420-421` "Sahifalar" ro'yxati esa
**§4 "UI (WPF)"** sarlavhasi ostida — ya'ni hujjat hamon o'lik WPF loyihasini
"prezentatsiya qatlami" deb hisoblaydi. O'zini "HAKAM" deb e'lon qilgan hujjat
(`CONTRACT.md:3-4`) asosiy dasturni bilmaydi.

**D-02. Nusxa ko'chirib ishlatib bo'lmaydigan buyruqlar — `-SelfContained` olib
tashlangan.**
Skriptlarda parametr `[switch] $FrameworkDependent` ga almashtirilgan
(`build/publish-windows.ps1:38`, `build/publish.ps1:37`), lekin **to'rt joyda eski
parametr bilan buyruq yozilgan va u bugun ishlamaydi**:
- `README.md:203` — `.\build\publish-windows.ps1 -SelfContained $false`
- `docs/CHIQARISH.md:187`, `:224-225`, `:243`
- `build/README.md:293` — bundan yomoni, bu **eski WPF skriptini** o'lik parametr
  bilan chaqirishni o'rgatadi.
`build/README.md:190-194` esa o'zgarishni **to'g'ri** hujjatlashtirgan — ya'ni
`build/README.md` o'z ichida qarama-qarshi.

**D-03. "O'quv yillari" ekrani va bir nechta jadval varianti hech qayerda yozilmagan.**
- `CONTRACT.md:420-421` — 9 ta sahifa sanaydi, "O'quv yillari" yo'q (10 ta bo'lishi kerak).
- `FOYDALANISH.md:11-13` — 8 qadamli ish oqimida jadval varianti tanlash qadami yo'q.
- **Eng muhimi:** `MainWindow.axaml:82-121` dagi yuqori paneldagi ikkita tanlagich
  ("O'quv yili" va "Dars jadvali") **barcha sahifalarning ma'lumotini jimgina
  chegaralaydi**, lekin foydalanuvchi qo'llanmasida bu haqda **bir og'iz ham yo'q**.
  Foydalanuvchi "darslarim yo'qolib qoldi" deb o'ylashi mumkin — aslida boshqa
  jadval varianti faol.

**D-04. `README.md:5` "internet talab qilinmaydi" — noto'g'ri.**
`AboutViewModel.cs:98` — sahifa ochilishi bilanoq **avtomatik ravishda**
github.com ga HTTPS so'rov yuboriladi (`AppInfo.cs:44-52`). Bu real, foydalanuvchiga
ko'rinadigan funksiya (`AboutView.axaml:59-124`), lekin **hech bir hujjatda
tilga olinmagan** va README'ning va'dasiga zid.

**D-05. macOS baza yo'li ikki hujjatda noto'g'ri.**
`README.md:221` va `CONTRACT.md:409` — `~/.local/share/DarsJadvali/darsjadvali.db`.
Amalda macOS'da .NET `LocalApplicationData` ni
`~/Library/Application Support` ga hal qiladi va baza haqiqatan o'sha yerda.
`ARXITEKTURA.md:132`, `CHIQARISH.md:331-332`, `FOYDALANISH.md:30`,
`AVALONIA-KOCHIRISH.md:161`, `build/README.md:327` — **to'g'ri** yozgan.
Xato manbasi ehtimol `InfrastructureServiceRegistration.cs:16-18` dagi XML izoh —
u ham xato.

**D-06. `AVALONIA-KOCHIRISH.md` — `App.axaml` "AYNAN shunday" deb noto'g'ri
namuna beradi.**
`:31-44` — "**AYNAN shunday** (tasdiqlangan)" deb 10 qatorli namuna keltiradi,
lekin haqiqiy `App.axaml` da namunada yo'q ikkita zarur element bor:
`<Application.DataTemplates><local:ViewLocator /></Application.DataTemplates>`
(8-10-qatorlar) va
`<StyleInclude Source="avares://DarsJadvali/Styles/AppStyles.axaml" />` (15-qator).
Hujjatga so'zma-so'z amal qilgan odam **butun ViewModel→View bog'lanishini buzadi**.
Bundan tashqari `ARXITEKTURA.md:199-201` ga ham zid (u to'g'ri yozgan).

### 7.3 🟠 `CONTRACT.md` qoidalari buzilgan joylar

**D-07. `CONTRACT.md:435` — "har bir `async` metod `CancellationToken ct = default`
bilan tugaydi".** Bu qoida **deyarli faqat prezentatsiya qatlamida** buzilgan
(Application/Infrastructure unga rioya qiladi). ~35 ta buzilish, jumladan:
- `Desktop/Services/IDialogService.cs:9, 12, 15, 18, 21, 24, 27` — **yettala** metod
- `MainViewModel.cs:216, 266, 291`
- `TimetableViewModel.cs:254, 385 (public!), 423, 539, 551`
- `AcademicYearsViewModel.cs:244, 350, 415, 431, 593`
- `AboutViewModel.cs:104, 109, 163, 167, 184`
- `AssignmentsViewModel.cs:152, 192, 209, 221, 356`, `DashboardViewModel.cs:381`,
  `ClassGroupsViewModel.cs:117, 231`, `SubjectsViewModel.cs:124, 240`,
  `TeachersViewModel.cs:128, 220`, `AvailabilityViewModel.cs:134, 255`,
  `WorkDaysViewModel.cs:90, 133`, `App.axaml.cs:86`

Qiziq tomoni: `DashboardViewModel.cs:566` va `TimetableViewModel.cs:599`
(`ExportPdfAsync`) **qoidaga rioya qiladi** — ya'ni kod o'z ichida ham izchil emas.
*Qaror kerak:* yo qoidani ViewModel'larga ham majburiy qilib ~35 joyni tuzatish,
yo qoidaning qamrovini hujjatda aniq cheklash. Bu M-03 (token uzatilmaydi)
muammosi bilan bevosita bog'liq.

**D-08. `CONTRACT.md` pinlagan turlar o'zgartirilgan** (hujjat `:3-4` da buni
"mumkin emas" degan edi):
- `CONTRACT.md:107-118` `ScheduleEntry` — 9 a'zo; amalda `ScheduleEntry.cs:10-13`
  da `required ScheduleId` + `Schedule` navigatsiyasi qo'shilgan.
- `CONTRACT.md:214-221` `ScheduleEntryDraft` — 7 parametrli record; amalda 8
  (`ScheduleEntryDraft.cs:16-24`).
- `CONTRACT.md:391` — unikal indekslar `ScheduleId` qo'shilgach majburan o'zgargan.

**D-09. `ARXITEKTURA.md:193` — "View — faqat ko'rinish. Kod-behind'da mantiq yo'q".**
`Views/DashboardView.axaml.cs` — **319 qator imperativ to'r qurish** (M-04).
Ikkinchi hakam hujjat esa buni **ochiq ruxsat etadi**:
`AVALONIA-KOCHIRISH.md:109` — "Kataklarni kod tomondan (ViewModel yoki code-behind)
joylashtiring". Ikki hujjat bir-biriga zid; yolg'oni — `ARXITEKTURA.md` dagi mutlaq
da'vo.

**D-10. `AVALONIA-KOCHIRISH.md` ko'chirish tugagani haqida jim.**
Hujjat hamon buyruq mayli ("ko'chiring va moslashtiring", `:86-98`) bilan yozilgan,
go'yo ish hali boshlanmagandek. Amalda ko'chirish **tugagan**: 10 ta ekran
`.axaml` da, v1.0.0 `Desktop` dan chiqarilgan, `publish/` da tayyor DMG va ZIP lar bor.
**Hech bir hujjatda `DarsJadvali.UI` ni o'chirish rejasi yoki muddati yo'q.**
Eng kuchli ifoda — `ARXITEKTURA.md:150-154`: "Solution ichida saqlanib turibdi" —
bu qaror emas, tavsif. `build/README.md:44-45` uni shunchaki "eski Windows-only
versiya" deydi va **funksional farqni kamsitib ko'rsatadi** (§1.2 ga qarang).

**D-11. `AVALONIA-KOCHIRISH.md:135-147` — `IDialogService` 6 ta metod deydi.**
Amalda 7 ta: `SaveFileAsync(...)` qo'shilgan (`IDialogService.cs:27`) — bu PDF
eksport uchun zarur bo'lgan, va hujjatning o'zi `:164-165` da PDF eksport haqida gapiradi.

### 7.4 🟡 Sanoq nomuvofiqliklari (mexanik tuzatish)

| Hujjat | Da'vo | Haqiqat |
|---|---|---|
| `ARXITEKTURA.md:80` | "8 ta entity" | 10 (`AcademicYear`, `Schedule` qo'shilgan) |
| `README.md:76-80` | 8 ta entity sanaydi | 10 |
| `ARXITEKTURA.md:112`, `CONTRACT.md:389` | "8 ta `DbSet<>`" | 10 (`AppDbContext.cs:13-22`) |
| `README.md:118-123`, `ARXITEKTURA.md:346-350` | 4 ta test fayli | **9** (`AcademicYearServiceTests`, `ScheduleSetServiceTests`, `DatabaseMigrationTests`, `LessonAvailabilityTests`, `PdfExportTests`, `UpdateCheckerTests` qo'shilgan) |
| `ARXITEKTURA.md:102`, `README.md:83` | `Abstractions/` 3 ta interfeys | 4 (`IUpdateChecker.cs`) |
| `README.md:91-98`, `ARXITEKTURA.md:109-132` | Infrastructure papkalari | `Update/` papkasi yo'q |
| `README.md:113-115` | Web `Endpoints/` | `ScheduleSetEndpoints.cs`, `SettingsEndpoints.cs` sanalmagan |
| Hech qayerda | — | `PDFsharp 6.2.4` hech bir paket jadvalida yo'q |
| `ARXITEKTURA.md:318-321` | `DashboardViewModel` `IEnumerable<IScheduleGenerator>` oladi | bitta `IScheduleGenerator` (`DashboardViewModel.cs:107`) — matn kelajak zamonda, chalg'itadi |

### 7.5 ✅ Hujjatlar to'g'ri bo'lgan joylar (buzmaslik kerak)

- **Va'da qilinib bajarilmagan funksiya YO'Q.** Drag-drop, Excel/HTML eksport,
  o'rinbosarlik moduli, talaba/xona ko'rinishlari — hech biri va'da qilinmagan.
  `FOYDALANISH.md:242-243` hatto ochiq yozgan: *"Katakni sudrab ko'chirish hozircha
  ishlamaydi"*. Bu — halollik, saqlab qolish kerak.
- `AVALONIA-KOCHIRISH.md:26-29` paket versiyalari — `.csproj` bilan aniq mos.
- `AVALONIA-KOCHIRISH.md:56-67` WPF→Avalonia moslik jadvali — tekshirildi,
  Desktop'da birorta `Visibility` bindingi va `Items=` yo'q.
- `AVALONIA-KOCHIRISH.md:69-80` compiled bindings — `x:DataType` **13 ta View va
  31 ta `DataTemplate` ning hammasida** bor.
- `ARXITEKTURA.md:33-62` qatlamlar diagrammasi va bog'liqlik jadvali — to'g'ri.
- `CONTRACT.md:435` "faqat o'zbekcha UI matni" — 13 ta `.axaml` faylning barcha
  `Content=`/`Text=`/`Watermark=`/`Header=` atributlari tekshirildi: **100% o'zbek lotin**.
- `build/README.md:21-45, 289-303` — eski `publish.ps1` natijasi
  `publish/legacy-wpf/` ga ajratilgan va ZIP nomi `DarsJadvali-legacy-wpf-*`;
  nom to'qnashuvi haqiqatan hal qilingan.
- `CONTRACT.md:427` / `README.md:165` — `http://localhost:5080` to'g'ri.

---

## 8. Prezentatsiya qatlami uchun qayta qurish rejasi

### 8.0 Boshlang'ich holat

Desktop qatlami **sifatli yozilgan, lekin noto'g'ri arxitekturaga qurilgan**:
`CommunityToolkit.Mvvm` to'g'ri ishlatilgan, ViewModel'da `DbContext` yo'q,
bloklovchi chaqiruvlar yo'q, compiled bindings yoqilgan. Muammo — **jadval
ko'rinishining o'zi**: u "ma'lumot jadvali + forma" modeliga qurilgan, aSc esa
"tirik kartalar maydoni" modelini talab qiladi. Bu farq shunchalik tubdanki,
mavjud `TimetableView` ni "yaxshilab" bo'lmaydi — uni **qayta yozish** kerak.

---

### 0-bosqich — Tozalash (1-2 kun, xavfsiz, darhol)

Maqsad: keyingi ishga toza maydon.

| # | Ish | Fayllar |
|---|---|---|
| 0.1 | **`DarsJadvali.UI` ni `.sln` dan chiqarish va o'chirish** | `DarsJadvali.sln`, `src/DarsJadvali.UI/**` (60 fayl) |
| 0.2 | Eski yig'ish skriptlarini o'chirish | `build/publish.ps1`, `build/publish.bat` |
| 0.3 | **E-01 tuzatish** — o'qituvchi rejimida PDF butun maktabni bermasin | `TimetableViewModel.cs:606-610` |
| 0.4 | **E-02 tuzatish** — `SuggestFileName` `options` ni hisobga olsin | `SchoolTimetablePdfExporter.cs:55-59` |
| 0.5 | **U-01 tuzatish** — `Process.Start` dan oldin `https` + `github.com` tekshiruvi | `GitHubUpdateChecker.cs:183, 378` yoki `AboutViewModel.cs:167` |
| 0.6 | **M-08 / U-02 tuzatish** — versiyani assembly'dan olish, `const` → `static readonly` | `AppInfo.cs:10`, `index.html:234` |
| 0.7 | **U-03 tuzatish** — `HttpResponseMessage` sizishi | `GitHubUpdateChecker.cs:318-338` |
| 0.8 | Hujjatlarni D-02, D-05 bo'yicha tuzatish (ishlamaydigan buyruqlar, macOS yo'li) | `README.md:203, 221`, `CHIQARISH.md:187, 224-225, 243`, `build/README.md:293`, `CONTRACT.md:409`, `InfrastructureServiceRegistration.cs:16-18` |

**Natija:** build vaqti qisqaradi, `DarsJadvali.exe` chalkashligi yo'qoladi,
uchta real xato yopiladi.

---

### 1-bosqich — Poydevorni mustahkamlash (1-2 hafta)

Bu bosqichsiz drag-drop qurish **xavfli**: har sudrash DB yozuvini keltirib
chiqaradi va M-01 (parallel `DbContext`) darhol portlaydi.

**1.1 `DbContext` konkurentligini hal qilish (M-01, M-02) — MAJBURIY**
- `IDbContextFactory<AppDbContext>` ga o'tish yoki har amal uchun alohida qamrov.
- `ViewModelBase` ga `RunAsync(Func<CancellationToken, Task>, string errorMessage)`
  yordamchisi: `IsBusy`, `try/catch`, `OperationCanceledException`, dialog, **va
  `SemaphoreSlim` bilan seriyalash** — hammasi bir joyda (M-18 ni ham yopadi).
- Barcha `[RelayCommand]` larga `CanExecute = nameof(IsNotBusy)`.

**1.2 Navigatsiya va bekor qilish (M-03, M-07)**
- `INavigationService.NavigateToAsync(Type vmType, object? parameter, CancellationToken)`.
- Har navigatsiyada eski `CancellationTokenSource.Cancel()`.
- `MainViewModel.PendingClassGroupId` ni o'chirib, parametrni navigatsiya orqali uzatish.

**1.3 ViewModel'ni UI turlaridan tozalash (M-06)**
- `IBrush`, `Thickness`, `Color` ni ViewModel'dan olib tashlash;
  `ScheduleColors` ni `Converters/` ga ko'chirish yoki XAML `Style` selektorlariga
  aylantirish (`Border.cell:selected`).
- Natija: ViewModel'lar Avalonia'siz test qilinadi va **qora mavzu** imkoni ochiladi.

**1.4 Prezentatsiya testlari (hozir 0 ta)**
- `tests/DarsJadvali.Desktop.Tests` — ViewModel testlari (Avalonia'siz, 1.3 dan keyin).
- `Avalonia.Headless.XUnit` — `TimetableView` uchun asosiy o'zaro ta'sir testlari.

**1.5 Fayl tuzilmasini tartibga solish**
- `ViewModels/ClassTimetableViewModel.cs` dagi 6 ta ochiq turni ajratish.
- `UniqueViolation` (`SubjectsViewModel.cs:279`) ni Infrastructure'ga ko'chirish (M-19).
- `AcademicYearsViewModel` (685) ni ikkiga bo'lish: yillar / jadval variantlari.

---

### 2-bosqich — Jadval yadrosini qayta yozish (3-4 hafta) ⭐ ASOSIY ISH

**2.1 Yangi ma'lumot modeli**

Yassi `ObservableCollection<TimetableCellViewModel>` ni tashlash. O'rniga:

```
TimetableBoardViewModel          // butun maydon
 ├─ IReadOnlyList<DayColumn>     // kunlar (ustunlar)
 ├─ IReadOnlyList<LessonRow>     // dars soatlari (qatorlar)
 ├─ ObservableCollection<CardViewModel>   // KARTALAR — kataklar emas
 └─ SelectionState / DragState / HighlightState
```

Muhim farq: **karta (`CardViewModel`) katakdan mustaqil obyekt**. Karta
`(Day, Lesson)` koordinatasiga ega, lekin to'r qayta qurilganda **yashaydi**.
Shundagina sudrash, qulflash va ko'p tanlov mumkin bo'ladi.

**2.2 Render: `Canvas` yoki `ItemsRepeater`**

| Variant | Ijobiy | Salbiy |
|---|---|---|
| **`Canvas` + `Canvas.Left/Top` binding** | Sudrash tabiiy (piksel darajasida), animatsiya oson, karta to'r ustida "suzadi" | Virtualizatsiya qo'lda, o'lchash qo'lda |
| `ItemsRepeater` + maxsus `Layout` | Avalonia 11 da virtualizatsiya tayyor | Sudrash paytida element ko'chirish murakkab |

**Tavsiya:** to'r fonini (chiziqlar, kun/dars sarlavhalari) alohida qatlamda
chizish, kartalarni esa ustidagi `Canvas` da joylashtirish. Bu aSc'ning o'zi
qo'llagan model.

**2.3 Drag & drop**
- `DragDrop.DoDragDrop` yoki qo'lda `PointerPressed`/`PointerMoved`/`PointerReleased`
  (Avalonia'da ikkinchisi ko'proq nazorat beradi).
- Sudrash boshlanganda — `IScheduleValidator` dan **barcha bo'sh joylar uchun**
  natijani bir marta olish (batch), keyin yoritishni xotiradan chizish.
  Har harakatda DB ga bormaslik — bu ishlash tezligining kaliti.
- Tashlanganda — `IScheduleService.MoveAsync` (**Application qatlamida allaqachon
  bor**, `ScheduleEndpoints.cs:36` uni ochadi, lekin hech bir UI ishlatmaydi — W-07).

**2.4 Jonli to'qnashuv va "mumkin bo'lgan pozitsiyalar"**
- `IScheduleValidator` ga `GetCandidateSlotsAsync(draft)` kabi **paketli** metod
  kerak (Application qatlami vazifasi — boshqa agent bilan kelishilsin).
- Yashil = mumkin, sariq = ogohlantirish bilan mumkin, qizil = mumkin emas.
- Karta ustida turganda tooltip'da to'qnashuv sababi.

**2.5 Undo / Redo**
- `ICommand` naqshi emas — **amallar jurnali**: `IUndoableAction { Do(); Undo(); }`.
- Amallar: qo'yish, o'chirish, ko'chirish, tozalash, avtomatik tuzish.

**2.6 Karta qulflash (card lock)**
- `ScheduleEntry` ga `IsLocked` maydoni kerak (Domain — boshqa agent).
- UI: qulf ikonasi, qulflangan kartani sudrab bo'lmaydi, avtomatik tuzish uni tegmaydi.

**2.7 Ko'rinishlar (view modes)**
Hozir 2 ta (sinf, o'qituvchi). Kerak: **sinf | o'qituvchi | xona | fan**.
Xona ko'rinishi uchun `RoomNumber` (matn) o'rniga to'laqonli `Room` entity kerak
(Domain — boshqa agent).

---

### 3-bosqich — Bosh sahifa va umumiy ko'rinish (1-2 hafta)

**3.1 `DashboardView.axaml.cs` dagi 319 qatorni yo'q qilish (M-04)**
- 2-bosqichdagi `TimetableBoardViewModel` ni **qayta ishlatish** — bosh sahifa
  shunchaki "faqat o'qish rejimidagi kengaytirilgan taxta" bo'lsin.
- Bu kod dublikatini (§4.1) butunlay yopadi.
- Virtualizatsiya majburiy: 40 sinf × 8 dars × 6 kun ni virtualizatsiyasiz
  chizib bo'lmaydi.

**3.2 Mavzu va ranglar (M-15, M-16, M-17)**
- `AppStyles.axaml` dagi ikki marta yozilgan uslublarni birlashtirish.
- Barcha `StaticResource` → `DynamicResource`.
- Qora mavzu (1.3 dan keyin mumkin bo'ladi).
- `MenuItemModel.IconKind` ni nihoyat chizish (M-14) — `Material.Avalonia`
  allaqachon ulangan.

---

### 4-bosqich — Eksport va chop etish (2-3 hafta)

**4.1 Eksport abstraksiyasi (E-05, E-06)**
```
ITimetableExporter          // format: PDF | HTML | XLSX | CSV
ITimetableLayoutTemplate    // dizayn: stacked-grid | per-class | per-teacher | per-room
TimetableExportRequest      // kim uchun: school | class | teacher | room | student
```
Hozirgi `SchoolTimetablePdfExporter` — shu abstraksiyaning **bitta**
implementatsiyasi bo'lib qoladi.

**4.2 Yetishmayotgan variantlar (muhimlik tartibida)**
1. **O'qituvchi jadvali PDF** — E-01 ni tabiiy yopadi, UI'da rejim allaqachon bor.
2. Bitta sinf uchun alohida bir sahifalik varaq (hozirgisi — stacked-grid nusxasi).
3. HTML eksport (brauzerda ochish, chop etish) — `index.html:190-213` dagi
   chop etish CSS'i tayyor namuna.
4. Excel/CSV.
5. Xona jadvali (2.7 dan keyin).

**4.3 Sifat tuzatishlari**
- E-03: sinf nomi va kun sarlavhalarini o'lchash/o'rash; limit tugaganda `…` qo'yish.
- E-04: sahifadan baland qatorni bo'lish yoki shriftni kichraytirish.
- E-08: `Render` ni `Task.Run` ichiga olish — UI muzlashini yo'qotish.
- E-05: A4 o'lchamini bitta manbaga keltirish (`:168` vs `:176-177`).
- E-10: `.resx` lokalizatsiya poydevori (o'zbek + rus + ingliz — maktablar uchun real talab).
- E-11: `LICENSE-DejaVu.txt` ga `CopyToOutputDirectory`.

---

### 5-bosqich — Hujjatlar (doimiy, har bosqich oxirida)

| # | Ish |
|---|---|
| 5.1 | `CONTRACT.md` ga `DarsJadvali.Desktop` bo'limini qo'shish; §4 (WPF) ni o'chirish (D-01) |
| 5.2 | `CONTRACT.md:435` `ct` qoidasi bo'yicha **qaror**: yo ~35 joyni tuzatish, yo qoida qamrovini cheklash (D-07) |
| 5.3 | `CONTRACT.md` dagi pinlangan turlarni `ScheduleId` bilan yarashtirish (D-08) |
| 5.4 | `FOYDALANISH.md` ga "O'quv yillari va jadval variantlari" bo'limi + yuqori paneldagi tanlagichlar izohi (D-03) |
| 5.5 | Yangilanish tekshiruvini hujjatlashtirish va `README.md:5` ni tuzatish (D-04) |
| 5.6 | `AVALONIA-KOCHIRISH.md` ni **tarix** sifatida qayta sarlavhalash; `App.axaml` namunasini tuzatish (D-06, D-10) |
| 5.7 | `ARXITEKTURA.md:193` ni `AVALONIA-KOCHIRISH.md:109` bilan yarashtirish (D-09) |
| 5.8 | Sanoqlarni yangilash: entity 8→10, `DbSet` 8→10, testlar 4→9 (D, §7.4) |

---

### 8.1 Xulosa — nima birinchi

```
0-bosqich (1-2 kun)   → tozalash + 3 ta real xato        ← DARHOL
1-bosqich (1-2 hafta) → DbContext konkurentligi, DI, testlar  ← MAJBURIY POYDEVOR
2-bosqich (3-4 hafta) → jadval yadrosini qayta yozish     ← ASOSIY QIYMAT
3-bosqich (1-2 hafta) → bosh sahifa + mavzu
4-bosqich (2-3 hafta) → eksport abstraksiyasi
5-bosqich (doimiy)    → hujjatlar
```

**1-bosqichni o'tkazib yuborib 2-bosqichga o'tmang.** Drag-drop har harakatda
validatsiya so'raydi; hozirgi bitta `Scoped DbContext` modeli bunga bardosh bermaydi
va foydalanuvchi "A second operation started on this context" xatosini
sudrashning har uchinchi urinishida ko'radi.

### 8.2 Boshqa agentlar bilan kelishish kerak bo'lgan nuqtalar

Quyidagilar prezentatsiya qatlamidan **tashqarida**, lekin usiz aSc darajasiga
chiqib bo'lmaydi:

| Talab | Qaysi qatlam |
|---|---|
| `IScheduleValidator.GetCandidateSlotsAsync(draft)` — paketli "qayerga qo'yish mumkin" | Application |
| `ScheduleEntry.IsLocked` (card lock) | Domain + Persistence |
| To'laqonli `Room` entity (hozir — `RoomNumber` matni) | Domain + Persistence |
| Talaba / guruh (seminar group) modeli | Domain + Persistence |
| `IDbContextFactory<AppDbContext>` yoki konkurentlikka chidamli qamrov modeli | Infrastructure |
| SQLite `WAL` + `busy_timeout` (Web bilan birga ishlash uchun — W-02) | Infrastructure |
| O'rinbosarlik (substitution) modeli | Domain + Application |
