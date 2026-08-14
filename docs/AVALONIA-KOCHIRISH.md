# Avalonia ko'chirish qo'llanmasi — MAJBURIY

> Bu hujjat `DarsJadvali.Desktop` (Avalonia) loyihasi uchun HAKAM.
> Quyidagi retseptlar **shu Mac'da haqiqiy ishga tushirib tasdiqlangan** — ularni
> o'zgartirmang, "yaxshiroq" variant izlab vaqt yo'qotmang.

Maqsad: **macOS uchun alohida dastur, Windows uchun alohida dastur.** Ikkalasi bir xil
vazifani bajaradi, bitta manba kodidan yig'iladi:

```
DarsJadvali.app  (macOS: arm64 + x64)
DarsJadvali.exe  (Windows: x64 + x86)
```

`Domain`, `Application`, `Infrastructure` qatlamlari **o'zgarmaydi** — ular allaqachon
platformadan mustaqil va sinovdan o'tgan.

---

## 1. Tasdiqlangan paketlar va sozlash

`.csproj` allaqachon yaratilgan (o'zgartirmang). Paketlar:

| Paket | Versiya |
|---|---|
| Avalonia, Avalonia.Desktop, Avalonia.Fonts.Inter, Avalonia.Controls.DataGrid | 11.2.3 |
| Material.Avalonia, Material.Avalonia.DataGrid | 3.9.2 |
| CommunityToolkit.Mvvm | 8.3.2 |
| Microsoft.Extensions.Hosting | 8.0.1 |

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
**Avalonia'da bu yo'q.** To'g'ri yechim — butun jadvalni **bitta `Grid`** qilib qurish:

- `ColumnDefinitions` = `Sinf` | `Soat` | har bir faol ish kuni uchun bittadan
- `RowDefinitions` = 1 ta sarlavha qatori + (sinflar soni × maksimal dars soati)
- Sinf nomi katagi: `Grid.RowSpan="{maksimal dars soati}"`
- Kataklarni kod tomondan (`ViewModel` yoki code-behind) joylashtiring

Bu foydalanuvchi so'ragan ko'rinishga aynan mos keladi:

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
`ScrollViewer` dan **tashqarida** qo'ying va ustun kengliklarini ikkalasida ham
**bir xil piksel qiymatlar** bilan bering (`SharedSizeGroup` o'rniga).

---

## 5. Dialoglar — `IDialogService`

Avalonia'da `MessageBox` yo'q. Quyidagi interfeys yoziladi (tashqi paketsiz):

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
  - macOS: `~/Library/Application Support/DarsJadvali/darsjadvali.db`
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
