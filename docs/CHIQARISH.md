# Reliz chiqarish qo'llanmasi

Bu hujjat **"Dars Jadvali Tuzuvchi"** dasturining yangi versiyasini tayyorlab,
foydalanuvchilarga tarqatish tartibini tushuntiradi.

Dastur ikkita alohida ko'rinishda tarqatiladi — ikkalasi ham **bitta manba koddan**
(`src/DarsJadvali.Desktop`, Avalonia) yig'iladi:

| Platforma | Fayl | Kim uchun |
|---|---|---|
| macOS | `DarsJadvali.app` (DMG ichida) | Mac egalari |
| Windows | `DarsJadvali.exe` (ZIP ichida) | Windows 10 / 11 |

> **Muhim cheklov:** macOS dasturi **faqat Mac'da**, Windows dasturi **faqat
> Windows'da** yig'iladi. `codesign` va `hdiutil` Windows'da yo'q; `.exe`
> yig'ish esa Windows SDK'siz to'liq ishlamaydi. Ya'ni to'liq reliz uchun
> ikkala kompyuter ham kerak bo'ladi.

---

## 0. Reliz oldidan tekshiruv ro'yxati

```bash
# 1. Barcha testlar o'tadimi
dotnet test

# 2. Dastur yig'iladimi
dotnet build src/DarsJadvali.Desktop/DarsJadvali.Desktop.csproj -v q

# 3. Dastur ochiladimi (oyna 5 soniyada o'zini yopadi)
DARSJADVALI_AUTOCLOSE=5 dotnet run --project src/DarsJadvali.Desktop
```

Uchalasi ham muvaffaqiyatli bo'lgandan keyingina relizga o'ting.

---

## 1. Versiya raqamini o'zgartirish

Versiya **bitta joyda** turadi va hamma yerga o'sha yerdan tarqaladi:

### `Directory.Build.props` (loyiha ildizi) — ASOSIY JOY

```xml
<PropertyGroup>
  ...
  <Version>1.0.0</Version>     <!-- ← SHU YERNI o'zgartiring -->
</PropertyGroup>
```

Bu qiymatni avtomatik ravishda o'qiydi va ishlatadi:

| Qayerda | Nima uchun |
|---|---|
| `build/publish-macos.sh` | `Info.plist` dagi `CFBundleShortVersionString` va `CFBundleVersion` |
| `build/publish-macos.sh` | DMG fayl nomi: `DarsJadvali-1.0.0-macos-arm64.dmg` |
| `build/publish-windows.ps1` | ZIP fayl nomi: `DarsJadvali-1.0.0-win-x64-selfcontained.zip` |
| `dotnet build` | `.exe` / `.dll` metama'lumotlari |

### `build/Info.plist.template` — odatda TEGILMAYDI

Bu faylda versiya o'rniga `__VERSION__` joy egallovchisi turadi —
`publish-macos.sh` uni `Directory.Build.props` dagi qiymat bilan almashtiradi.

Bu shablonni faqat **boshqa** bundle metama'lumoti o'zgarsa tahrirlang:

| Kalit | Hozirgi qiymat |
|---|---|
| `CFBundleName` | `DarsJadvali` |
| `CFBundleDisplayName` | `Dars Jadvali Tuzuvchi` |
| `CFBundleIdentifier` | `uz.abduxalilvoxidjonov.darsjadvali` |
| `CFBundleExecutable` | `DarsJadvali` |
| `LSMinimumSystemVersion` | `11.0` (macOS Big Sur va undan yangi) |
| `NSHighResolutionCapable` | `true` (Retina ekran uchun) |

> `CFBundleIdentifier` ni **hech qachon o'zgartirmang** — macOS uni dasturning
> doimiy identifikatori sifatida ishlatadi.

---

## 2. macOS relizi

### Buyruq

```bash
# Loyiha ildizidan, Mac'da:
bash build/publish-macos.sh
```

Standart holatda **ikkala** arxitektura yig'iladi va DMG yasaladi.
Faqat bittasi kerak bo'lsa:

```bash
bash build/publish-macos.sh --arch arm64      # faqat Apple Silicon
bash build/publish-macos.sh --arch x64        # faqat Intel
bash build/publish-macos.sh --no-dmg          # DMG'siz, faqat .app (sinov uchun)
```

Yig'ish 3–8 daqiqa vaqt oladi (birinchi marta NuGet paketlari yuklanadi).

### Natija qayerda

```
publish/
├── osx-arm64/DarsJadvali.app
├── osx-x64/DarsJadvali.app
├── DarsJadvali-1.0.0-macos-arm64.dmg     ← TARQATILADIGAN FAYL
└── DarsJadvali-1.0.0-macos-x64.dmg       ← TARQATILADIGAN FAYL
```

Foydalanuvchiga **DMG** fayl beriladi (`.app` ni to'g'ridan-to'g'ri emas —
`.app` bir necha ming fayldan iborat papka, u pochta orqali yuborilmaydi).

### Qaysi DMG kimga

| Fayl | Kimga |
|---|---|
| `...-macos-arm64.dmg` | **Apple Silicon** — M1, M2, M3, M4 protsessorli Mac. 2020-yil oxiridan keyin chiqqan deyarli barcha Mac'lar. |
| `...-macos-x64.dmg` | **Intel** protsessorli Mac (eski modellar). |

Foydalanuvchi qaysi Mac ekanini bilmasa:
**Apple menyusi () → About This Mac →** `Chip` yoki `Processor` qatori.
`Apple M...` → arm64. `Intel...` → x64.

> Ikkilanilsa `x64` versiyasini bering — u Rosetta 2 orqali Apple Silicon
> Mac'da ham ishlaydi (biroz sekinroq). Teskarisi ishlamaydi.

### FOYDALANUVCHIGA BERILADIGAN KO'RSATMA — buni tashlab ketmang

Dastur Apple sertifikati bilan imzolanmagan (sertifikat pullik). Shuning uchun
**birinchi marta ochish maxsus tartibda bo'ladi.** Bu matnni foydalanuvchiga
DMG bilan birga yuboring:

> **Dasturni birinchi marta ochish**
>
> 1. Yuklab olingan `DarsJadvali-...dmg` faylni ikki marta bosib oching.
> 2. Ichidagi **DarsJadvali** belgisini **Applications** (Dasturlar) papkasiga
>    sudrab tashlang.
> 3. Finder'ni oching → chap ustundan **Applications** ni tanlang.
> 4. **DarsJadvali** ustiga **o'ng tugma** bosing → menyudan **Open** (Ochish)
>    ni tanlang.
> 5. Chiqqan ogohlantirish oynasida yana **Open** tugmasini bosing.
>
> Shundan keyin dastur odatdagidek ikki marta bosish bilan ochilaveradi.
>
> **Nega shunday?** Dastur Apple'ning pullik sertifikati bilan imzolanmagan.
> Oddiy ikki marta bosilsa macOS `"unidentified developer"` yoki
> `"cannot be opened"` deb ogohlantiradi. Bu **dasturdagi nosozlik emas** —
> yuqoridagi o'ng tugma → Open usuli aynan shuning uchun kerak.
>
> Agar macOS baribir ochmasa:
> **System Settings → Privacy & Security** → pastga tushing →
> `"DarsJadvali was blocked..."` yozuvi yonidagi **Open Anyway** tugmasi.

### Yig'ilgan `.app` ni o'zingiz tekshirish

```bash
# Info.plist to'g'ri XML mi
plutil -lint publish/osx-arm64/DarsJadvali.app/Contents/Info.plist

# Imzo qo'yilganmi (Signature=adhoc ko'rinishi kerak)
codesign -dv --verbose=2 publish/osx-arm64/DarsJadvali.app

# Ishga tushuvchi fayl bajariluvchimi
ls -l publish/osx-arm64/DarsJadvali.app/Contents/MacOS/DarsJadvali

# Haqiqatan ochiladimi
open publish/osx-arm64/DarsJadvali.app
```

---

## 3. Windows relizi

### Buyruq

```powershell
# Loyiha ildizidan, Windows'da:
.\build\publish-windows.ps1
```

Standart holatda **x64 va x86** ikkalasi self-contained rejimda yig'iladi.

```powershell
.\build\publish-windows.ps1 -Runtime win-x64                      # faqat 64-bit
.\build\publish-windows.ps1 -Runtime win-x86                      # faqat 32-bit
.\build\publish-windows.ps1 -SelfContained $false                 # kichik hajm, .NET alohida
```

Skript ishga tushmasa:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build\publish-windows.ps1
```

### Natija qayerda

```
publish\
├── win-x64\DarsJadvali.exe
├── win-x86\DarsJadvali.exe
├── DarsJadvali-1.0.0-win-x64-selfcontained.zip    ← TARQATILADIGAN FAYL
└── DarsJadvali-1.0.0-win-x86-selfcontained.zip    ← TARQATILADIGAN FAYL
```

### x64 va x86 farqi

| Fayl | Kimga |
|---|---|
| `win-x64` | **64-bitli Windows** — bugungi deyarli barcha kompyuterlar |
| `win-x86` | **32-bitli Windows** — eski kompyuterlar |

Foydalanuvchi qaysi ekanini bilmasa:
**Sozlamalar → Tizim → Haqida →** `Tizim turi` qatori (yoki `Win + Pause`).

> Ikkilanilsa `win-x86` ni bering — u **64-bitli Windows'da ham ishlaydi**.
> Teskarisi ishlamaydi. Kamchiligi: 4 GB dan ortiq xotira ishlata olmaydi
> (bu dastur uchun muammo emas).

### .NET talabi — WPF versiyasidan MUHIM farq

| Rejim | Foydalanuvchida nima kerak |
|---|---|
| `-SelfContained $true` (standart) | **Hech narsa.** ZIP ni ochib `.exe` ni bosish kifoya. |
| `-SelfContained $false` | **.NET 8 Runtime** |

> ### DIQQAT
> Avalonia versiyasi **`.NET 8 Runtime`** talab qiladi —
> **`.NET Desktop Runtime` EMAS.**
>
> Eski WPF versiyasi (`publish.ps1`) Desktop Runtime talab qilardi, chunki WPF
> Windows'ning grafik kutubxonalariga bog'liq. Avalonia esa o'z grafik qatlamini
> olib yuradi va WPF'ga umuman bog'liq emas.
>
> Yuklab olish: https://dotnet.microsoft.com/download/dotnet/8.0/runtime
> Bo'lim: **.NET Runtime 8.x** (Desktop Runtime bo'limi emas)
> - 64-bit: `dotnet-runtime-8.x.x-win-x64.exe`
> - 32-bit: `dotnet-runtime-8.x.x-win-x86.exe`
>
> Kompyuterda `.NET Desktop Runtime` allaqachon o'rnatilgan bo'lsa, dastur
> baribir ishlaydi — Desktop Runtime o'z ichiga oddiy .NET Runtime'ni oladi.

**Tavsiya:** maktabga tarqatishda **doim `-SelfContained $true`** (standart)
ishlating. Hajmi kattaroq, lekin foydalanuvchi hech narsa o'rnatmaydi.

### FOYDALANUVCHIGA BERILADIGAN KO'RSATMA

> **Dasturni o'rnatish**
>
> 1. ZIP faylni yuklab oling.
> 2. Ustiga o'ng tugma → **Extract All / Barchasini chiqarish**.
>    (ZIP ichidan to'g'ridan-to'g'ri ishga tushirmang!)
> 3. Chiqqan papkadagi **DarsJadvali.exe** ni ikki marta bosing.
>
> **"Windows protected your PC" oynasi chiqsa:**
> **More info** (Batafsil) → **Run anyway** (Baribir ishga tushirish).
>
> Bu dastur kod-imzo sertifikati bilan imzolanmagani uchun chiqadi —
> nosozlik emas. Bir marta shunday qilingandan keyin qayta chiqmaydi.

---

## 4. Imzolash — hozirgi holat va kelajak

### Hozir

| Platforma | Holat | Natija |
|---|---|---|
| macOS | **Ad-hoc imzo** (`codesign --sign -`), bepul | `"damaged app"` xatosi yo'q, lekin birinchi ochishda o'ng tugma → Open kerak |
| Windows | **Imzosiz** | SmartScreen ogohlantirishi: More info → Run anyway |

Maktab miqyosidagi tarqatish uchun bu holat **yetarli**.

### Kelajakda — Apple sertifikati olinsa

Sertifikat: **Apple Developer Program**, yiliga $99 —
https://developer.apple.com/programs/

Kerak bo'ladigan narsalar:
1. `Developer ID Application` sertifikati (Xcode → Settings → Accounts →
   Manage Certificates)
2. App-specific parol — https://appleid.apple.com → Sign-In and Security
3. Team ID (10 belgi) — https://developer.apple.com/account → Membership

Buyruq:

```bash
# Avval odatdagidek yig'ing
bash build/publish-macos.sh --no-dmg

# So'ng har bir arxitekturani imzolang va notarizatsiya qiling
bash build/sign-macos.sh \
  --identity "Developer ID Application: Ism Familiya (TEAMID123)" \
  --app publish/osx-arm64/DarsJadvali.app \
  --apple-id pochta@example.com \
  --team-id TEAMID123 \
  --password "abcd-efgh-ijkl-mnop"

# Imzolangan .app dan keyin DMG ni qayta yasang
```

Nima kerakligini eslash uchun sertifikatsiz ham ishga tushirsa bo'ladi —
skript xato bermaydi, ko'rsatma chiqaradi:

```bash
bash build/sign-macos.sh
```

**Notarizatsiyadan keyin:** foydalanuvchi dasturni **oddiy ikki marta bosish**
bilan ochadi. O'ng tugma → Open ko'rsatmasi kerak bo'lmaydi va hech qanday
ogohlantirish chiqmaydi. Ya'ni yuqoridagi 2-bo'limdagi uzun ko'rsatmani
foydalanuvchiga yuborish shart bo'lmay qoladi.

### Windows kod-imzo sertifikati

Alohida sotib olinadi (DigiCert, Sectigo va h.k., yiliga ~$200–400).
Hozircha rejada yo'q. `signtool.exe` bilan qo'llaniladi.

---

## 5. Ma'lumotlar bazasi qayerda saqlanadi

Baza **dastur papkasida emas**, foydalanuvchi profilida saqlanadi. Shuning uchun
dasturni yangilash (eski papkani o'chirib, yangisini qo'yish) **ma'lumotlarni
yo'qotmaydi**.

| Platforma | Yo'l |
|---|---|
| **Windows** | `%LOCALAPPDATA%\DarsJadvali\darsjadvali.db` |
| | to'liq: `C:\Users\<Foydalanuvchi>\AppData\Local\DarsJadvali\darsjadvali.db` |
| **macOS** | `~/Library/Application Support/DarsJadvali/darsjadvali.db` |
| | to'liq: `/Users/<foydalanuvchi>/Library/Application Support/DarsJadvali/darsjadvali.db` |

Manba: `InfrastructureServiceRegistration.DefaultDbPath`
(`Environment.SpecialFolder.LocalApplicationData` orqali, cross-platform).

### Zaxira nusxa olish (foydalanuvchiga aytish uchun)

**macOS:** Finder → menyudan **Go → Go to Folder...** (`Shift+Cmd+G`) →
`~/Library/Application Support/DarsJadvali` → `darsjadvali.db` faylini nusxalang.

**Windows:** Explorer manzil qatoriga `%LOCALAPPDATA%\DarsJadvali` yozing →
`darsjadvali.db` faylini nusxalang.

> `.db` fayl bitta fayl — uni boshqa kompyuterga ko'chirsa, jadvallar ham
> o'sha yerga ko'chadi. Dastur yopiq holda nusxalang.

### To'liq tozalash (sinov uchun)

```bash
# macOS
rm -rf ~/Library/"Application Support"/DarsJadvali
```
```powershell
# Windows
Remove-Item -Recurse -Force "$env:LOCALAPPDATA\DarsJadvali"
```

---

## 6. Reliz tartibi — qisqacha

1. `Directory.Build.props` da `<Version>` ni oshiring.
2. `dotnet test` — barcha testlar o'tsin.
3. **Mac'da:** `bash build/publish-macos.sh` → 2 ta DMG.
4. **Windows'da:** `.\build\publish-windows.ps1` → 2 ta ZIP.
5. 4 ta faylni ham har bir platformada haqiqatan ochib ko'ring.
6. Fayllarni tarqating va **birinchi ochish ko'rsatmasini birga yuboring**
   (2-bo'lim macOS uchun, 3-bo'lim Windows uchun).

Skriptlar haqida batafsil: [`build/README.md`](../build/README.md).
