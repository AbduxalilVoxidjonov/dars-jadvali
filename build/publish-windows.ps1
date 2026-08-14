<#
.SYNOPSIS
    "Dars Jadvali Tuzuvchi" (Avalonia) dasturini Windows uchun yig'ib, ZIP arxivga joylaydi.

.DESCRIPTION
    Yangi cross-platform Avalonia loyihasi `src\DarsJadvali.Desktop` dan
    Windows 10 / Windows 11 uchun x64 va x86 (32-bitli) versiyalarni tayyorlaydi.
    Natija: <Output>\win-x64\DarsJadvali.exe, <Output>\win-x86\DarsJadvali.exe
    va ularning ZIP arxivlari.

    ESKI `publish.ps1` dan farqi: u WPF loyihasini (`src\DarsJadvali.UI`) yig'adi
    va foydalanuvchida .NET Desktop Runtime talab qiladi. Bu skript esa Avalonia
    loyihasini yig'adi - unga oddiy .NET 8 Runtime yetarli (Desktop Runtime EMAS).

.PARAMETER Runtime
    win-x64 | win-x86 | all   (standart: all)

.PARAMETER FrameworkDependent
    Berilmasa (STANDART): self-contained - .NET dastur ichiga qo'shib yuboriladi,
    foydalanuvchida hech narsa o'rnatilmasa ham ishlaydi (hajmi katta).
    Berilsa: framework-dependent - .NET 8 Runtime foydalanuvchi kompyuterida
    bo'lishi SHART (hajmi kichik).

.PARAMETER Output
    Natija papkasi (standart: <loyiha ildizi>\publish)

.EXAMPLE
    .\build\publish-windows.ps1
    .\build\publish-windows.ps1 -Runtime win-x86 -FrameworkDependent
    .\build\publish-windows.ps1 -Runtime all -Output C:\Temp\DarsJadvali
#>

[CmdletBinding()]
param(
    [ValidateSet("win-x64", "win-x86", "all")]
    [string] $Runtime = "all",

    [switch] $FrameworkDependent,

    [string] $Output = ""
)

$ErrorActionPreference = "Stop"

# --- Muhitni tekshirish -------------------------------------------------
# Windows PowerShell 5.1 da $IsWindows o'zgaruvchisi umuman mavjud emas ($null),
# lekin 5.1 faqat Windows'da ishlaydi. PowerShell 7 da esa u haqiqiy qiymat beradi.
$onWindows = if ($null -eq $IsWindows) { $true } else { [bool] $IsWindows }
if (-not $onWindows) {
    throw "Bu skript faqat Windows'da ishlaydi (Windows uchun .exe va ZIP yasaydi).`nmacOS uchun:  bash build/publish-macos.sh"
}

# --- Yo'llar ------------------------------------------------------------
$scriptDir      = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot    = Split-Path -Parent $scriptDir
$desktopProject = Join-Path $projectRoot "src\DarsJadvali.Desktop"

if ([string]::IsNullOrWhiteSpace($Output)) {
    $Output = Join-Path $projectRoot "publish"
}

if (-not (Test-Path $Output)) {
    New-Item -ItemType Directory -Path $Output -Force | Out-Null
}
# Nisbiy yo'l joriy papkaga bog'lanib qolmasin - absolyut yo'lga keltiramiz.
$Output = (Resolve-Path -LiteralPath $Output).ProviderPath

if (-not (Test-Path $desktopProject)) {
    throw "Avalonia loyihasi topilmadi: $desktopProject"
}

# --- .NET SDK bormi? ----------------------------------------------------
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if ($null -eq $dotnet) {
    throw ".NET SDK topilmadi. https://dotnet.microsoft.com/download/dotnet/8.0 dan .NET 8 SDK ni o'rnating."
}

$sdkVersion = (& dotnet --version)

# --- Versiya raqami (Directory.Build.props dan) -------------------------
$version = "1.0.0"
$propsPath = Join-Path $projectRoot "Directory.Build.props"
if (Test-Path $propsPath) {
    $m = Select-String -Path $propsPath -Pattern '<Version>(.*?)</Version>' | Select-Object -First 1
    if ($null -ne $m) {
        $version = $m.Matches[0].Groups[1].Value
    }
}

# Standart xatti-harakat: self-contained. -FrameworkDependent berilsagina teskari.
$selfContained = -not $FrameworkDependent

Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host " Dars Jadvali Tuzuvchi (Avalonia) - Windows uchun yig'ish" -ForegroundColor Cyan
Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host " .NET SDK versiyasi : $sdkVersion"
Write-Host " Loyiha             : $desktopProject"
Write-Host " Dastur versiyasi   : $version"
Write-Host " Natija papkasi     : $Output"
Write-Host " Rejim              : $(if ($selfContained) { 'self-contained (.NET ichida)' } else { 'framework-dependent (.NET alohida kerak)' })"
Write-Host ""

$runtimes = if ($Runtime -eq "all") { @("win-x64", "win-x86") } else { @($Runtime) }

$scLower = if ($selfContained) { "true" } else { "false" }
$created = @()

foreach ($rid in $runtimes) {
    $target = Join-Path $Output $rid

    Write-Host "---------------------------------------------------------" -ForegroundColor DarkGray
    Write-Host " $rid uchun yig'ilmoqda..." -ForegroundColor Yellow

    Write-Host " [$rid] 1/5  Eski natijalar tozalanmoqda..." -ForegroundColor DarkCyan
    if (Test-Path $target) {
        Remove-Item -Path $target -Recurse -Force
    }

    # Diqqat: siqish (EnableCompressionInSingleFile) va native kutubxonalarni ichiga
    # olish (IncludeNativeLibrariesForSelfExtract) FAQAT self-contained rejimda
    # qo'llab-quvvatlanadi. Framework-dependent publish'da bu bayroqlar bilan
    # `dotnet publish` xato beradi, shuning uchun ular shartli qo'shiladi.
    # `-p:DebugType=none` - ZIP ga DarsJadvali.pdb tushmasligi uchun.
    $publishArgs = @(
        "publish", $desktopProject,
        "-c", "Release",
        "-r", $rid,
        "--self-contained", $scLower,
        "-p:PublishSingleFile=true",
        "-p:DebugType=none",
        "-p:DebugSymbols=false"
    )
    if ($selfContained) {
        $publishArgs += "-p:IncludeNativeLibrariesForSelfExtract=true"
        $publishArgs += "-p:EnableCompressionInSingleFile=true"
    }
    $publishArgs += @("-o", $target)

    $rejim = if ($selfContained) { "self-contained" } else { "framework-dependent" }
    Write-Host " [$rid] 2/5  dotnet publish ($rejim)..." -ForegroundColor DarkCyan
    & dotnet @publishArgs

    if ($LASTEXITCODE -ne 0) {
        throw "$rid uchun yig'ish muvaffaqiyatsiz tugadi (kod: $LASTEXITCODE)."
    }

    Write-Host " [$rid] 3/5  DarsJadvali.exe tekshirilmoqda..." -ForegroundColor DarkCyan
    $exePath = Join-Path $target "DarsJadvali.exe"
    if (-not (Test-Path $exePath)) {
        throw "Ishga tushuvchi fayl topilmadi: $exePath`nTekshiring: .csproj da <AssemblyName>DarsJadvali</AssemblyName> turibdimi?"
    }
    $exeMb = [math]::Round((Get-Item $exePath).Length / 1MB, 1)
    Write-Host "          DarsJadvali.exe yasaldi ($exeMb MB)"

    # --- ZIP arxiv ------------------------------------------------------
    $suffix = if ($selfContained) { "selfcontained" } else { "framework" }
    $zipPath = Join-Path $Output "DarsJadvali-$version-$rid-$suffix.zip"

    if (Test-Path $zipPath) {
        Remove-Item -Path $zipPath -Force
    }

    Write-Host " [$rid] 4/5  ZIP arxiv yasalmoqda: $zipPath" -ForegroundColor DarkCyan
    Compress-Archive -Path (Join-Path $target "*") -DestinationPath $zipPath -CompressionLevel Optimal

    Write-Host " [$rid] 5/5  ZIP arxiv tekshirilmoqda..." -ForegroundColor DarkCyan
    if (-not (Test-Path $zipPath)) {
        throw "ZIP arxiv yaratilmadi: $zipPath"
    }
    $zipBytes = (Get-Item $zipPath).Length
    if ($zipBytes -lt 1MB) {
        throw "ZIP arxiv juda kichik ($zipBytes bayt) - yig'ish to'liq bo'lmagan ko'rinadi: $zipPath"
    }
    $zipMb = [math]::Round($zipBytes / 1MB, 1)
    Write-Host " Tayyor: $rid  (ZIP $zipMb MB)" -ForegroundColor Green

    $created += [PSCustomObject]@{
        Rid    = $rid
        Folder = $target
        Zip    = $zipPath
        ExeMb  = $exeMb
        ZipMb  = $zipMb
    }
}

Write-Host ""
Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host " YAKUN - nima yig'ildi" -ForegroundColor Cyan
Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host (" {0,-8}  {1,-8}  {2,-8}  {3}" -f "RID", "EXE", "ZIP", "FAYL")
Write-Host (" {0,-8}  {1,-8}  {2,-8}  {3}" -f "---", "---", "---", "----")
foreach ($item in $created) {
    Write-Host (" {0,-8}  {1,-8}  {2,-8}  {3}" -f $item.Rid, "$($item.ExeMb) MB", "$($item.ZipMb) MB", $item.Zip)
}

Write-Host ""
Write-Host "---------------------------------------------------------" -ForegroundColor Cyan
Write-Host " QAYSI FAYL QAYSI KOMPYUTER UCHUN" -ForegroundColor Cyan
Write-Host "---------------------------------------------------------" -ForegroundColor Cyan
Write-Host " win-x64  ->  64-bitli Windows (bugungi deyarli barcha kompyuterlar)"
Write-Host " win-x86  ->  32-bitli Windows (eski kompyuterlar)"
Write-Host ""
Write-Host " Foydalanuvchi qaysi ekanini bilmasa:"
Write-Host "   Sozlamalar -> Tizim -> Haqida -> `"Tizim turi`" qatori"
Write-Host "   (yoki: Win+Pause tugmalari)"
Write-Host "   Ikkilanilsa win-x86 ni bering - u 64-bitli Windows'da ham ishlaydi."

Write-Host ""
if ($selfContained) {
    Write-Host " Bu versiya SELF-CONTAINED: foydalanuvchi kompyuteriga hech narsa" -ForegroundColor Green
    Write-Host " o'rnatish SHART EMAS. Arxivni ochib, DarsJadvali.exe ni ishga" -ForegroundColor Green
    Write-Host " tushirish kifoya." -ForegroundColor Green
}
else {
    Write-Host " DIQQAT! Bu versiya FRAMEWORK-DEPENDENT." -ForegroundColor Yellow
    Write-Host " Foydalanuvchi kompyuterida .NET 8 RUNTIME o'rnatilgan bo'lishi SHART:" -ForegroundColor Yellow
    Write-Host "   https://dotnet.microsoft.com/download/dotnet/8.0/runtime" -ForegroundColor Yellow
    Write-Host ""
    Write-Host " MUHIM - qaysi runtime kerakligiga e'tibor bering:" -ForegroundColor Yellow
    Write-Host "   Kerak    : `".NET Runtime 8.x`"  ->  dotnet-runtime-8.x.x-win-x64.exe" -ForegroundColor Yellow
    Write-Host "                                       dotnet-runtime-8.x.x-win-x86.exe" -ForegroundColor Yellow
    Write-Host "   Kerak EMAS: `".NET Desktop Runtime`" (u eski WPF versiyasi uchun edi)" -ForegroundColor Yellow
    Write-Host ""
    Write-Host " Avalonia o'z grafik qatlamini olib yuradi, WPF'ga (ya'ni Windows'ning" -ForegroundColor Yellow
    Write-Host " Desktop Runtime'iga) bog'liq emas. Desktop Runtime o'rnatilgan bo'lsa," -ForegroundColor Yellow
    Write-Host " u .NET Runtime'ni o'z ichiga oladi - shunda ham ishlaydi." -ForegroundColor Yellow
}

Write-Host ""
Write-Host " SmartScreen ogohlantirishi:" -ForegroundColor Yellow
Write-Host "   Dastur kod-imzo sertifikati bilan imzolanmagan, shuning uchun birinchi"
Write-Host "   ishga tushirishda Windows `"Windows protected your PC`" oynasini"
Write-Host "   ko'rsatishi mumkin. Foydalanuvchi `"More info`" -> `"Run anyway`""
Write-Host "   tugmalarini bosishi kerak. Bu normal holat."

Write-Host ""
Write-Host " Ma'lumotlar bazasi foydalanuvchi kompyuterida shu yerda yaratiladi:"
Write-Host "   %LOCALAPPDATA%\DarsJadvali\darsjadvali.db"
Write-Host ""
Write-Host " macOS versiyasi uchun:  bash build/publish-macos.sh"
Write-Host ""
