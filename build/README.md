# `build/` — yig'ish va ishga tushirish skriptlari

Bu papkadagi skriptlar dasturni tayyor holga keltiradi (macOS va Windows uchun)
hamda sinov serverini ishga tushiradi.

Talab: **.NET 8 SDK** — https://dotnet.microsoft.com/download/dotnet/8.0

---

## Skriptlar ro'yxati

### Yangi — cross-platform Avalonia dasturi (`src/DarsJadvali.Desktop`)

| Fayl | Nima qiladi | Qayerda ishlaydi |
|------|-------------|------------------|
| `publish-macos.sh` | `DarsJadvali.app` bundle + DMG yasaydi (`osx-arm64`, `osx-x64`) | **faqat macOS** (bash) |
| `publish-windows.ps1` | `DarsJadvali.exe` + ZIP yasaydi (`win-x64`, `win-x86`) | **faqat Windows** (PowerShell) |
| `sign-macos.sh` | Apple sertifikati bilan to'liq imzo + notarizatsiya | macOS (kelajakda) |
| `Info.plist.template` | `.app` bundle uchun `Info.plist` shabloni (skript o'qiydi) | — |

### Eski — WPF dasturi (`src/DarsJadvali.UI`)

| Fayl | Nima qiladi | Qayerda ishlaydi |
|------|-------------|------------------|
| `publish.ps1` | WPF dasturini `win-x64` / `win-x86` uchun yig'adi va ZIP qiladi. Natija: `publish\legacy-wpf\` | Windows (PowerShell) |
| `publish.bat` | `publish.ps1` ni standart sozlamalar bilan chaqiradi (ESKI WPF!) | Windows (ikki marta bosish) |

> **DIQQAT — papka to'qnashuvi (tuzatilgan).**
> Ilgari `publish.ps1` ham, `publish-windows.ps1` ham bitta `publish\win-x64\`
> papkasiga, bitta xil `DarsJadvali.exe` nomi bilan yozar edi — ya'ni eski WPF
> natijasi yangi Avalonia natijasini jimgina almashtirib yuborardi.
> Endi eski WPF skripti **`publish\legacy-wpf\`** papkasiga yozadi va ZIP
> nomlarida ham `legacy-wpf` bo'ladi. Ikkalasi bir-biriga tegmaydi.

### Sinov serveri

| Fayl | Nima qiladi | Qayerda ishlaydi |
|------|-------------|------------------|
| `run-web.ps1` | Localhost test serverini ishga tushiradi | Windows / macOS / Linux |
| `run-web.sh` | Localhost test serverini ishga tushiradi | macOS / Linux / WSL |

> **Qaysi birini ishlatish kerak?**
> Yangi relizlar uchun **`publish-macos.sh` va `publish-windows.ps1`** —
> ikkalasi ham bitta Avalonia loyihasidan yig'iladi.
> `publish.ps1` (WPF) faqat eski Windows-only versiyani qayta yig'ish uchun qoldirilgan.

Reliz tayyorlashning to'liq tartibi: [`docs/CHIQARISH.md`](../docs/CHIQARISH.md).

---

## 1. `publish-macos.sh` — macOS dasturi

```bash
# Loyiha ildizidan:
bash build/publish-macos.sh
```

### Parametrlar

| Parametr | Qiymatlar | Standart | Izoh |
|----------|-----------|----------|------|
| `--arch` | `arm64`, `x64`, `both` | `both` | Qaysi protsessor uchun |
| `--output` | papka yo'li | `<ildiz>/publish` | Natija qayerga chiqsin |
| `--dmg` | — | yoqilgan | DMG obrazi yasalsin |
| `--no-dmg` | — | — | DMG yasalmasin, faqat `.app` |
| `--help` | — | — | Yordam |

### Namunalar

```bash
# Ikkala arxitektura + DMG (standart)
bash build/publish-macos.sh

# Faqat Apple Silicon, tezkor sinov uchun DMG'siz
bash build/publish-macos.sh --arch arm64 --no-dmg

# Boshqa papkaga
bash build/publish-macos.sh --output /tmp/chiqarish
```

### Natija

```
publish/
├── osx-arm64/
│   └── DarsJadvali.app          ← Apple Silicon (M1/M2/M3/M4)
├── osx-x64/
│   └── DarsJadvali.app          ← Intel Mac
├── DarsJadvali-1.0.0-macos-arm64.dmg
└── DarsJadvali-1.0.0-macos-x64.dmg
```

`.app` bundle tuzilmasi:

```
DarsJadvali.app/
└── Contents/
    ├── Info.plist               ← Info.plist.template dan yasaladi
    ├── PkgInfo
    ├── MacOS/                   ← dotnet publish natijasi (barcha fayllar)
    │   ├── DarsJadvali          ← ishga tushuvchi fayl (chmod +x)
    │   └── *.dylib, *.dll ...
    └── Resources/
        └── AppIcon.icns         ← ikonka topilsa
```

### Skript nima qiladi

1. `dotnet publish src/DarsJadvali.Desktop -c Release -r osx-<arch> --self-contained true -p:PublishSingleFile=false`
   **`PublishSingleFile` ataylab o'chirilgan** — `.app` bundle ichida u keraksiz va
   Avalonia'ning native kutubxonalari bilan muammo tug'diradi.
2. Qo'lda `.app` bundle yig'adi.
3. `Info.plist` ni shablondan yasaydi va `plutil -lint` bilan tekshiradi.
4. `chmod +x` va **ad-hoc imzo**: `codesign --force --deep --sign -`
   Bu bepul va `"DarsJadvali.app is damaged"` xatosini yo'qotadi.
5. `hdiutil create ... -format UDZO` bilan DMG yasaydi.
6. Foydalanuvchiga beriladigan ko'rsatmani chiqaradi.

### Ikonka (ixtiyoriy)

Skript quyidagi joylardan `.icns` faylni qidiradi (birinchi topilgani ishlatiladi):

```
build/AppIcon.icns
assets/AppIcon.icns
src/DarsJadvali.Desktop/Assets/AppIcon.icns
src/DarsJadvali.Desktop/Assets/DarsJadvali.icns
```

Topilmasa, macOS standart ikonkasini ko'rsatadi va bu xato hisoblanmaydi.

### MUHIM — birinchi ochish

Dastur Apple sertifikati bilan imzolanmagan. Foydalanuvchiga aytilishi **shart**:

> `.app` ga **o'ng tugma → Open → Open**.
> Oddiy ikki marta bosishda `"unidentified developer"` xatosi chiqadi — bu normal.

---

## 2. `publish-windows.ps1` — Windows dasturi

```powershell
# Loyiha ildizidan (Windows'da). PowerShell 7 (pwsh) tavsiya etiladi:
pwsh -NoProfile -ExecutionPolicy Bypass -File .\build\publish-windows.ps1
```

Skript **faqat Windows'da** ishlaydi — macOS/Linux'da ishga tushirilsa aniq
xato bilan to'xtaydi va `publish-macos.sh` ga yo'naltiradi.

### macOS/Linux'da Windows uchun yig'ish

Skript Windows'ni talab qiladi, lekin **`.exe` faylining o'zini macOS'da ham
yig'sa bo'ladi** — .NET kross-kompilyatsiyani qo'llab-quvvatlaydi. Windows faqat
dasturni **ishga tushirib sinash** va `signtool` bilan **imzolash** uchun kerak.
Skriptdagi bilan aynan bir xil natija beradigan buyruq:

```bash
dotnet publish src/DarsJadvali.Desktop -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:DebugType=none -p:DebugSymbols=false \
  -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true \
  -o publish/win-x64
```

`win-x86` uchun ham xuddi shunday. Natija — bitta `DarsJadvali.exe` (x64 ~49 MB,
x86 ~46 MB), yonida boshqa fayl qolmaydi.

Yig'ilganini tekshirish (macOS'da):

```bash
file publish/win-x64/DarsJadvali.exe
# PE32+ executable (GUI) x86-64, for MS Windows
```

`GUI` so'zi muhim — konsol oynasi ochilmasligini bildiradi. v1.0.0 relizidagi
Windows fayllari aynan shu yo'l bilan macOS'da yig'ilgan.

> **Diqqat:** bu usul faylni yig'adi, lekin uning **haqiqatan ishlashini
> tasdiqlamaydi**. Windows'da bir marta sinab ko'rmaguningizcha "ishlaydi" deb
> hisoblamang.

### Parametrlar

| Parametr | Qiymatlar | Standart | Izoh |
|----------|-----------|----------|------|
| `-Runtime` | `win-x64`, `win-x86`, `all` | `all` | Qaysi arxitektura |
| `-FrameworkDependent` | bayroq (switch) | berilmaydi | Berilsa `.NET` dastur ichiga QO'SHILMAYDI |
| `-Output` | papka yo'li | `<ildiz>\publish` | Natija qayerga chiqsin (nisbiy yo'l absolyutga aylantiriladi) |

> **`-SelfContained` parametri OLIB TASHLANDI.** Avval u `[bool]` edi va
> `-SelfContained false` deb yozilganda PowerShell bo'sh bo'lmagan `"false"`
> satrini `$true` ga aylantirib, foydalanuvchiga jimgina self-contained
> versiyani berardi. Endi standart xatti-harakat — self-contained; kichik
> hajmli versiya kerak bo'lsa `-FrameworkDependent` bayrog'ini bering.

### Natija

```
publish\
├── win-x64\DarsJadvali.exe      ← bitta fayl (single file)
├── win-x86\DarsJadvali.exe
├── DarsJadvali-1.0.0-win-x64-selfcontained.zip
└── DarsJadvali-1.0.0-win-x86-selfcontained.zip
```

ZIP ichiga papkadagi **hamma** fayl tushadi. `DarsJadvali.pdb` (nosozlik
tuzatish belgilari) tushib qolmasligi uchun skript `-p:DebugType=none` va
`-p:DebugSymbols=false` bilan publish qiladi — shuning uchun yuqoridagi
ro'yxat haqiqatga mos.

### Self-contained yoki framework-dependent?

| | standart (bayroqsiz) | `-FrameworkDependent` |
|---|---|---|
| Hajmi | ~70–100 MB | ~10–15 MB |
| Foydalanuvchida .NET kerakmi | **Yo'q** | **Ha** — .NET 8 **Runtime** |
| Tavsiya | Maktabga tarqatish uchun | IT bo'limi boshqaradigan tarmoq uchun |

### Namunalar

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\build\publish-windows.ps1
pwsh -NoProfile -ExecutionPolicy Bypass -File .\build\publish-windows.ps1 -Runtime win-x86 -FrameworkDependent
pwsh -NoProfile -ExecutionPolicy Bypass -File .\build\publish-windows.ps1 -Runtime all -Output C:\Temp\DarsJadvali
```

> ### DIQQAT — WPF versiyasidan MUHIM farq
> Avalonia dasturi **`.NET 8 Runtime`** talab qiladi, **`.NET Desktop Runtime` EMAS.**
> Avalonia o'z grafik qatlamini olib yuradi va Windows'ning WPF/WinForms
> kutubxonalariga bog'liq emas.
>
> Yuklab olish: https://dotnet.microsoft.com/download/dotnet/8.0/runtime
> → `dotnet-runtime-8.x.x-win-x64.exe` yoki `dotnet-runtime-8.x.x-win-x86.exe`
>
> (Desktop Runtime o'rnatilgan bo'lsa ham ishlaydi — u .NET Runtime'ni o'z ichiga oladi.)

### `EnableCompressionInSingleFile` haqida

`EnableCompressionInSingleFile` va `IncludeNativeLibrariesForSelfExtract`
**faqat self-contained** rejimda qo'llab-quvvatlanadi. Framework-dependent
publish'da bu bayroqlar bilan `dotnet publish` xato beradi, shuning uchun
skriptda ular shartli qo'shiladi (`publish.ps1` dagi yondashuv bilan bir xil).

### PowerShell "skript ishga tushmayapti" desa

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build\publish-windows.ps1
```

---

## 3. `sign-macos.sh` — to'liq imzo va notarizatsiya

**Hozircha kerak emas.** Bu skript Apple Developer Program obunasi (yiliga $99)
va `Developer ID Application` sertifikati bo'lgandagina ishlatiladi.

```bash
# Nima kerakligini ko'rish (sertifikatsiz ham ishlaydi, faqat ko'rsatma chiqaradi):
bash build/sign-macos.sh

# Sertifikat bo'lganda:
bash build/sign-macos.sh \
  --identity "Developer ID Application: Ism Familiya (TEAMID123)" \
  --app publish/osx-arm64/DarsJadvali.app \
  --apple-id pochta@example.com \
  --team-id TEAMID123 \
  --password "abcd-efgh-ijkl-mnop"
```

| Parametr | Izoh |
|----------|------|
| `--identity` | Sertifikat nomi (majburiy). Ro'yxat: `security find-identity -v -p codesigning` |
| `--app` | Imzolanadigan `.app` bundle (majburiy) |
| `--apple-id` | Apple ID pochtasi |
| `--team-id` | 10 belgili Team ID |
| `--password` | App-specific parol (appleid.apple.com dan) |
| `--no-notarize` | Faqat imzolash, notarizatsiyasiz |

Qadamlar: `codesign --options runtime --timestamp` → `ditto` ZIP →
`xcrun notarytool submit --wait` → `xcrun stapler staple`.

Notarizatsiyadan keyin foydalanuvchi dasturni **oddiy ikki marta bosish** bilan
ochadi — o'ng tugma → Open hiylasi kerak bo'lmaydi.

`--identity` berilmasa skript **xato bermaydi** — nima kerakligini tushuntirib chiqadi.

---

## 4. `publish.ps1` / `publish.bat` — eski WPF versiyasi

```powershell
.\build\publish.ps1
.\build\publish.ps1 -Runtime win-x86 -SelfContained $false
```

Parametrlari `publish-windows.ps1` bilan bir xil, lekin:

- Loyiha: `src\DarsJadvali.UI` (`net8.0-windows`, WPF)
- Framework-dependent rejimda **`.NET 8 Desktop Runtime`** talab qiladi
- **Faqat Windows'da** publish qilinadi (macOS/Linux'da `dotnet build` ishlashi
  mumkin, lekin `publish` qilinmaydi)

`publish.bat` — PowerShell bilan ishlashni xohlamaganlar uchun, ikki marta bosiladi.

---

## 5. `run-web.ps1` / `run-web.sh` — brauzerda sinash

```bash
bash build/run-web.sh   # macOS / Linux / WSL
```
```powershell
.\build\run-web.ps1     # Windows / PowerShell
```

Ishga tushgach brauzerda oching: **http://localhost:5080**. To'xtatish: `Ctrl + C`.

Bu server desktop dasturi bilan **bir xil** Application va Infrastructure
qatlamlarini ishlatadi — validatsiya va generator mantig'i aynan bir xil.

---

## Eslatmalar

- **Ma'lumotlar bazasi** foydalanuvchi kompyuterida yaratiladi:
  - Windows: `%LOCALAPPDATA%\DarsJadvali\darsjadvali.db`
  - macOS: `~/Library/Application Support/DarsJadvali/darsjadvali.db`
- **Versiya raqami** `Directory.Build.props` dagi `<Version>` da turadi.
  `publish-macos.sh` va `publish-windows.ps1` uni avtomatik o'qiydi va
  `Info.plist` hamda arxiv nomlariga qo'yadi.
- **macOS dasturini Windows'da yig'ib bo'lmaydi** va aksincha: `codesign`,
  `hdiutil` faqat macOS'da bor. Har bir platforma o'zida yig'iladi.
- Testlar: loyiha ildizida `dotnet test`.
