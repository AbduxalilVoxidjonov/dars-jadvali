# Avalonia ko'chirish — retseptlar va tarix

> ## HOLAT: KO'CHIRISH TUGALLANGAN
>
> `DarsJadvali.Desktop` (Avalonia) — **asosiy va yagona ish stoli dasturi**.
> Eski `src/DarsJadvali.UI` (WPF) `DarsJadvali.sln` **dan chiqarilgan**: papka diskda
> tarixiy nusxa sifatida turibdi, lekin yig'ilmaydi va unga tegilmaydi.
>
> Shu sababli bu hujjat endi **"nima qilish kerak" rejasi emas**, balki:
> 1. **Ishlaydigan retseptlar to'plami** (§1–§3, §5–§6) — yangi ekran yozayotganda kerak;
> 2. **Ko'chirish tarixi** — qanday qarorlar qabul qilingani.
>
> Ba'zi bo'limlar (ayniqsa **§4**) ko'chirish boshlanishida yozilgan va **amalda
> boshqacha hal qilingan** — o'sha joyda buni ochiq yozib qo'yganmiz.

Maqsad: **macOS uchun alohida dastur, Windows uchun alohida dastur.** Ikkalasi bir xil
vazifani bajaradi, bitta manba kodidan yig'iladi:

```
DarsJadvali.app  (macOS: arm64 + x64)
DarsJadvali.exe  (Windows: x64 + x86)
```

`Domain`, `Application`, `Infrastructure` qatlamlari **o'zgarmadi** — ular allaqachon
platformadan mustaqil va sinovdan o'tgan edi.

---

## 1. Tasdiqlangan paketlar va sozlash

`.csproj` allaqachon yaratilgan (o'zgartirmang). Paketlar —
`src/DarsJadvali.Desktop/DarsJadvali.Desktop.csproj` bilan tasdiqlangan:

| Paket | Versiya |
|---|---|
| Avalonia, Avalonia.Desktop, Avalonia.Fonts.Inter, Avalonia.Controls.DataGrid | 11.2.3 |
| Avalonia.Diagnostics (faqat `Debug`) | 11.2.3 |
| Material.Avalonia, Material.Avalonia.DataGrid | 3.9.2 |
| CommunityToolkit.Mvvm | 8.3.2 |
| Microsoft.Extensions.Hosting | 8.0.1 |

Muhim `PropertyGroup` qiymatlari: `OutputType=WinExe`, `AssemblyName=DarsJadvali`,
`AvaloniaUseCompiledBindingsByDefault=true`,
`RuntimeIdentifiers=osx-arm64;osx-x64;win-x64;win-x86`.

### `App.axaml` — AYNAN shunday (tasdiqlangan)

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:themes="clr-namespace:Material.Styles.Themes;assembly=Material.Styles"
             x:Class="DarsJadvali.Desktop.App"
             RequestedThemeVariant="Light">
  <Application.Styles>
    <themes:MaterialTheme BaseTheme="Light" PrimaryColor="DeepPurple" SecondaryColor="Teal" />
    <StyleInclude Source="avares://Material.Avalonia.DataGrid/MaterialDataGridStyles.axaml" />
  </Application.Styles>
</Application>
```

> Diqqat: `avares://Material.Avalonia/Material.Avalonia.Templates.xaml` **ISHLAMAYDI**
> (3.x da olib tashlangan). DataGrid stili yo'li ham aynan yuqoridagidek —
> `App.xaml` emas, `MaterialDataGridStyles.axaml`.

---

## 2. WPF → Avalonia farqlari (eng ko'p xato shu yerda)

| WPF | Avalonia 11 |
|---|---|
| `.xaml` | **`.axaml`** |
| `xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"` | `xmlns="https://github.com/avaloniaui"` |
| `Visibility="{Binding X, Converter=...}"` | **`IsVisible="{Binding X}"`** (oddiy `bool`) — visibility konverterlari KERAK EMAS, ularni ko'chirmang |
| `Items` / `ItemsSource` | har doim **`ItemsSource`** |
| `Grid.IsSharedSizeScope` + `SharedSizeGroup` | **YO'Q** — 4-bo'limga qarang |
| `MessageBox.Show(...)` | **YO'Q** — o'z dialog oynangizni yozing (5-bo'lim) |
| `Clipboard.SetText(s)` | `await TopLevel.GetTopLevel(control)!.Clipboard!.SetTextAsync(s)` |
| `<Style TargetType="Button">` | `<Style Selector="Button">`, sinf bilan: `Selector="Button.asosiy"` + `Classes="asosiy"` |
| `Dispatcher.Invoke` | `Dispatcher.UIThread.InvokeAsync` / `.Post` |
| `SolidColorBrush(ColorConverter...)` | `new SolidColorBrush(Color.Parse("#1976D2"))` |
| `Window.ShowDialog()` | `await window.ShowDialog(owner)` — **async** |
| `ContextMenu` | `<Button.ContextFlyout>` yoki `ContextMenu` (ikkalasi ham bor) |

### Kompilyatsiya vaqtidagi binding tekshiruvi — MAJBURIY

`.csproj` da `AvaloniaUseCompiledBindingsByDefault=true` yoqilgan. Ya'ni:

- Har bir `DataTemplate` va `UserControl` ga **`x:DataType`** qo'ying:
  ```xml
  <UserControl xmlns:vm="clr-namespace:DarsJadvali.Desktop.ViewModels"
               x:DataType="vm:TeachersViewModel" ...>
  ```
- Shunda **noto'g'ri binding nomi build'da xato beradi**, runtime'da jimgina ishlamay
  qolmaydi. Bu WPF versiyasida bo'lmagan katta ustunlik — undan to'liq foydalaning.
- `x:DataType` qo'yilmagan joyda binding ishlamasligi mumkin — unutmang.

---

## 3. ViewModel'lar

WPF versiyasidagi ViewModel'lar (`src/DarsJadvali.UI/ViewModels/`) mantiq jihatdan
tayyor va to'g'ri — ularni **ko'chiring va moslashtiring**, qaytadan o'ylab topmang.

O'zgarishi kerak bo'lgan yagona joylar:
- `System.Windows.*` / `System.Windows.Media.*` `using` lari → `Avalonia.*`
- `Visibility` qaytaradigan propertylar → `bool`
- `MessageBox` chaqiruvlari → `IDialogService` (5-bo'lim)
- `Clipboard` → `IDialogService.CopyToClipboardAsync`
- Namespace: `DarsJadvali.UI.ViewModels` → `DarsJadvali.Desktop.ViewModels`

`CommunityToolkit.Mvvm` (`[ObservableProperty]`, `[RelayCommand]`) ikkalasida ham
bir xil ishlaydi — o'zgartirish shart emas.

---

## 4. Maktab jadvali to'ri — `SharedSizeGroup` siz

WPF versiyasida ustunlarni tekislash uchun `SharedSizeGroup` ishlatilgan.
**Avalonia'da bu yo'q.**

> ### ⚠️ BU BO'LIM ESKIRGAN — amalda boshqacha qilindi
>
> Quyidagi "butun jadvalni bitta `Grid` qilib qurish" tavsiyasi ko'chirish
> boshlanishida yozilgan va **qabul qilinmadi**. Sabab: bitta `Grid` da
> `sinflar × dars soatlari` kataklarining **hammasi** bir vaqtda quriladi —
> 30 sinfli maktabda bu minglab element degani va interfeys sekinlashadi.
>
> **Haqiqiy yechim — virtualizatsiya.** `Views/TimetableBoardView.axaml` da
> qatorlar ham, joylashtirilmagan kartalar paneli ham **`VirtualizingStackPanel`**
> ichida: ekranda ko'rinmagan qator **umuman qurilmaydi**.
> Ustun kengliklari `ViewModels/TimetableMetrics.cs` dagi **hisoblangan piksel
> qiymatlar** bilan tekislanadi (zoom 50–200% va "Zich/Oddiy/Keng" zichligiga
> qarab qayta hisoblanadi) — bu `SharedSizeGroup` ning o'rnini bosadi.
>
> Yangi to'r yozayotgan bo'lsangiz **`TimetableBoardView.axaml` va
> `TimetableBoardViewModel.cs` dan nusxa oling**, quyidagi tavsifdan emas.

<details>
<summary>Dastlabki (amalga oshmagan) reja — tarix uchun</summary>

- `ColumnDefinitions` = `Sinf` | `Soat` | har bir faol ish kuni uchun bittadan
- `RowDefinitions` = 1 ta sarlavha qatori + (sinflar soni × maksimal dars soati)
- Sinf nomi katagi: `Grid.RowSpan="{maksimal dars soati}"`
- Kataklarni kod tomondan (`ViewModel` yoki code-behind) joylashtiring

```
┌───────┬─────────┬──────────┬──────────┬────────────┐
│ SINF  │  SOAT   │ Dushanba │ Seshanba │ Chorshanba │
├───────┼─────────┼──────────┼──────────┼────────────┤
│       │ 1-soat  │   Mat    │   Fiz    │    Ona     │
│  5-A  │ 2-soat  │   Ona    │   Mat    │    Ing     │
│       │ 7-soat  │          │          │            │
├───────┼─────────┼──────────┼──────────┼────────────┤
│  5-B  │ 1-soat  │   Ing    │   Mat    │    Fiz     │
└───────┴─────────┴──────────┴──────────┴────────────┘
```

Sarlavha qatori scroll qilganda joyida qolishi uchun: sarlavhani alohida `Grid` da
`ScrollViewer` dan **tashqarida** qo'yish va ustun kengliklarini ikkalasida ham
bir xil piksel qiymatlar bilan berish.

</details>

### 4.1 Amalda qurilgan jadval taxtasi

Fayllar: `Views/TimetableBoardView.axaml(.cs)`, `ViewModels/TimetableBoardViewModel.cs`,
`Services/Timetable/`.

| Xususiyat | Qanday qilingan |
|---|---|
| Virtualizatsiya | `VirtualizingStackPanel` (qatorlar + joylashtirilmaganlar paneli) |
| Ko'chirish | **"Karta qo'lda" (card-in-hand)** — `Services/Timetable/DragSession.cs`. HTML5 drag-drop **EMAS**: `PointerPressed` bilan olinadi, `PointerMoved` bilan yuradi, yana `PointerPressed` bilan qo'yiladi. `PointerReleased` ishlatilmaydi |
| Baholash | `TimetableBoard.Evaluate(...)` → `PlacementRating` (`Forbidden`/`Allowed`/`Preferred`) → konverter → kulrang/ko'k/yashil |
| Modifikatorlar | **SHIFT** — mumkin joylarni yoritish; **CTRL** — bir `LessonKey` dagi kartalarni birga olish; **ESC** — bekor qilish |
| Undo/redo | `Services/Timetable/CommandHistory.cs`, `DefaultLimit = 100`. `Ctrl+Z` / `Ctrl+Y` (`Ctrl+Shift+Z`) |
| Zoom | `ViewModels/TimetableMetrics.cs` — `MinZoom = 0.5`, `MaxZoom = 2.0`, `ZoomStep = 0.1`; `Ctrl+0` — qaytarish |
| Smena | `RebuildShifts()` — smena tanlagichi faqat `ShiftList.Count > 1` bo'lsa ko'rinadi |

> **Rang qoidasi (M-06):** ViewModel `IBrush` **qaytarmaydi** — u `PlacementRating`
> yoki `"#RRGGBB"` satr beradi, rangni `Converters/` dagi konverter hal qiladi.
> Batafsil: [`CONTRACT.md`](CONTRACT.md) §1.4.

> **Bir vaqtdalik qoidasi:** sahifaning barcha async amallari
> `Services/AsyncOperationRunner.cs` orqali o'tadi — bitta DI qamrovida bitta
> `DbContext` bo'lgani uchun ikki amal bir vaqtda ishlay olmaydi.
> Batafsil: [`CONTRACT.md`](CONTRACT.md) §1.5.

---

## 5. Dialoglar — `IDialogService`

Avalonia'da `MessageBox` yo'q. Quyidagi interfeys **yozilgan va ishlatilmoqda**
(`src/DarsJadvali.Desktop/Services/`, tashqi paketsiz):

```csharp
namespace DarsJadvali.Desktop.Services;

public interface IDialogService
{
    Task InfoAsync(string message, string title = "Ma'lumot");
    Task ErrorAsync(string message, string title = "Xato");
    Task<bool> ConfirmAsync(string message, string title = "Tasdiqlang");
    Task ShowValidationAsync(ValidationResult result);      // Error qizil, Warning sariq
    Task<bool> ConfirmWarningsAsync(ValidationResult result); // "Baribir qo'yilsinmi?"
    Task CopyToClipboardAsync(string text);
}
```

Implementatsiya: kichik `Window` (`DialogWindow.axaml`) + `await ShowDialog<bool>(owner)`.
Egasi (owner) — `IClassicDesktopStyleApplicationLifetime.MainWindow`.

---

## 6. macOS bilan bog'liq nozikliklar

- **Havolani ochish:** `Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })`
  — macOS'da ham ishlaydi, alohida kod kerak emas.
- **Ma'lumotlar bazasi yo'li:** `InfrastructureServiceRegistration.DefaultDbPath`
  allaqachon cross-platform (`Environment.SpecialFolder.LocalApplicationData`):
  - Windows: `%LOCALAPPDATA%\DarsJadvali\darsjadvali.db`
  - macOS: **`~/Library/Application Support/DarsJadvali/darsjadvali.db`**
  - Linux: **`~/.local/share/DarsJadvali/darsjadvali.db`**

  > **Diqqat.** .NET macOS'da `LocalApplicationData` ni **`~/Library/Application
  > Support`** ga moslashtiradi — bu macOS'ning odatiy joyi. Foydalanuvchiga
  > yo'lni aytayotganda shuni bering (papka Finder'da yashirin —
  > `Shift+Cmd+G` bilan oching).
- **Menyu paneli:** macOS'da menyu ekran tepasida bo'ladi. Hozircha `NativeMenu`
  qo'shmang — keraksiz murakkablik.
- **Fayl saqlash dialogi** (PDF eksport uchun): `TopLevel.GetTopLevel(control)!
  .StorageProvider.SaveFilePickerAsync(...)` — WPF'dagi `SaveFileDialog` o'rniga.
- **Shrift:** `Avalonia.Fonts.Inter` qo'shilgan. O'zbek lotin harflari (`oʻ`, `gʻ`)
  to'g'ri chiqishini albatta tekshiring.

---

## 7. Ishga tushirish va tekshirish

```bash
dotnet build src/DarsJadvali.Desktop/DarsJadvali.Desktop.csproj -v q
dotnet run --project src/DarsJadvali.Desktop
```

**Muhim:** `dotnet run` GUI oynasini ochadi va terminalni bloklaydi. Avtomatik
tekshirish uchun dasturni bir necha soniyadan keyin o'zini yopadigan qilib ishga
tushiring (muhit o'zgaruvchisi `DARSJADVALI_AUTOCLOSE=5` ni qo'llab-quvvatlang —
`Program.cs` da o'qib, berilgan soniyadan keyin `Shutdown(0)` qiling). Bu faqat
sinov uchun; o'zgaruvchi berilmasa dastur normal ishlaydi.

Shu yo'l bilan **oyna haqiqatan ochilganini va ishga tushishda istisno
bo'lmaganini** tasdiqlash mumkin — WPF'da bunday imkoniyat yo'q edi.

---

## 8. Ma'lum cheklovlar

| Cheklov | Tafsilot |
|---|---|
| **Sudrab ko'chirish qo'lda sinalmagan** | Jadval taxtasidagi "karta qo'lda" mexanikasi (§4.1) kod darajasida yozilgan va mantiqiy testlar bilan qoplangan, lekin **haqiqiy sichqoncha bilan uchdan-uchgacha sinov o'tkazilmagan**. `DARSJADVALI_AUTOCLOSE` faqat oyna ochilishini tekshiradi, o'zaro ta'sirni emas |
| **Ikkita jadval ekrani** | Chap menyudagi eski **"Dars jadvali"** (`TimetableViewModel`) va Bosh sahifadagi yangi **jadval taxtasi** (`TimetableBoardViewModel`) yonma-yon turibdi. Eskisida **undo/redo yo'q** — u `CommandHistory` dan o'tmay, to'g'ridan-to'g'ri `_board.MoveCard(card, null)` chaqiradi va bazadan qayta yuklaydi |
| **`RemoveCardCommand`** | `Services/Timetable/TimetableCommands.cs` da e'lon qilingan, lekin **hech qayerda ishlatilmaydi** — o'lik kod |
| **`TimeLimit` UI'da yo'q** | Generatsiya vaqt chegarasi Web API'da bor (`TimeLimitSeconds`), Desktop'da esa sozlanmaydi — faqat "Bekor qilish" tugmasi |
| **`NativeMenu`** | macOS'ning yuqori menyu paneli qo'shilmagan (ataylab — keraksiz murakkablik) |
| **Eski WPF loyihasi** | `src/DarsJadvali.UI` diskda qolgan, lekin `.sln` da yo'q va yig'ilmaydi. Uni yig'adigan `build/publish.ps1` / `publish.bat` skriptlari ham eskirgan |
