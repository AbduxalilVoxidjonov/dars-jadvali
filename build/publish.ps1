<#
.SYNOPSIS
    ESKI (WPF) "Dars Jadvali Tuzuvchi" dasturini Windows uchun yig'ib, ZIP arxivga joylaydi.

.DESCRIPTION
    DIQQAT: bu skript ESKI WPF loyihasini (`src\DarsJadvali.UI`) yig'adi.
    Yangi (Avalonia) dastur uchun `publish-windows.ps1` ishlating.

    Windows 10 va Windows 11 uchun x64 va x86 (32-bitli) versiyalarni tayyorlaydi.
    Natija: <Output>\win-x64\, <Output>\win-x86\ papkalari va ularning ZIP arxivlari.
    Standart chiqish papkasi: <loyiha ildizi>\publish\legacy-wpf
    (yangi Avalonia natijalari bilan TO'QNASHMASLIGI uchun alohida papka).

.PARAMETER Runtime
    win-x64 | win-x86 | all   (standart: all)

.PARAMETER FrameworkDependent
    Berilmasa (STANDART): self-contained - .NET ichiga qo'shib yuboriladi,
    foydalanuvchida hech narsa o'rnatilmasa ham ishlaydi (hajmi katta).
    Berilsa: framework-dependent - .NET 8 DESKTOP Runtime foydalanuvchi
    kompyuterida bo'lishi SHART (hajmi kichik).

.PARAMETER Output
    Natija papkasi (standart: <loyiha ildizi>\publish\legacy-wpf)

.EXAMPLE
    .\publish.ps1
    .\publish.ps1 -Runtime win-x86 -FrameworkDependent
    .\publish.ps1 -Runtime all -Output C:\Temp\DarsJadvaliEski
#>

[CmdletBinding()]
param(
    [ValidateSet("win-x64", "win-x86", "all")]
    [string] $Runtime = "all",

    [switch] $FrameworkDependent,

    [string] $Output = ""
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "*********************************************************" -ForegroundColor Yellow
Write-Host " OGOHLANTIRISH" -ForegroundColor Yellow
Write-Host " Bu ESKI WPF dasturini yig'adi. Yangi (Avalonia) dastur" -ForegroundColor Yellow
Write-Host " uchun publish-windows.ps1 ishlating." -ForegroundColor Yellow
Write-Host "*********************************************************" -ForegroundColor Yellow
Write-Host ""

# --- Yo'llar ------------------------------------------------------------
$scriptDir   = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$uiProject   = Join-Path $projectRoot "src\DarsJadvali.UI"

if ([string]::IsNullOrWhiteSpace($Output)) {
    # MUHIM: `publish\win-x64` ga YOZILMAYDI - u yerda publish-windows.ps1
    # yasagan yangi Avalonia natijalari turadi va exe nomi ham bir xil
    # (DarsJadvali.exe). Almashib ketmasligi uchun alohida papka.
    $Output = Join-Path (Join-Path $projectRoot "publish") "legacy-wpf"
}

if (-not (Test-Path $uiProject)) {
    throw "WPF loyihasi topilmadi: $uiProject"
}

# --- .NET SDK bormi? ----------------------------------------------------
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if ($null -eq $dotnet) {
    throw ".NET SDK topilmadi. https://dotnet.microsoft.com/download/dotnet/8.0 dan .NET 8 SDK ni o'rnating."
}

# Standart xatti-harakat: self-contained. -FrameworkDependent berilsagina teskari.
$selfContained = -not $FrameworkDependent

$sdkVersion = (& dotnet --version)
Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host " Dars Jadvali Tuzuvchi (ESKI WPF) - yig'ish (publish)" -ForegroundColor Cyan
Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host " .NET SDK versiyasi : $sdkVersion"
Write-Host " Loyiha             : $uiProject"
Write-Host " Natija papkasi     : $Output"
Write-Host " Rejim              : $(if ($selfContained) { 'self-contained (.NET ichida)' } else { 'framework-dependent (.NET alohida kerak)' })"
Write-Host ""

$runtimes = if ($Runtime -eq "all") { @("win-x64", "win-x86") } else { @($Runtime) }

if (-not (Test-Path $Output)) {
    New-Item -ItemType Directory -Path $Output -Force | Out-Null
}
# Nisbiy yo'l joriy papkaga bog'lanib qolmasin - absolyut yo'lga keltiramiz.
$Output = (Resolve-Path -LiteralPath $Output).ProviderPath

$scLower = if ($selfContained) { "true" } else { "false" }
$created = @()

foreach ($rid in $runtimes) {
    $target = Join-Path $Output $rid

    Write-Host "---------------------------------------------------------" -ForegroundColor DarkGray
    Write-Host " $rid uchun yig'ilmoqda..." -ForegroundColor Yellow

    if (Test-Path $target) {
        Write-Host " Eski papka tozalanmoqda: $target"
        Remove-Item -Path $target -Recurse -Force
    }

    # Diqqat: siqish (EnableCompressionInSingleFile) va native kutubxonalarni ichiga olish
    # FAQAT self-contained rejimda qo'llab-quvvatlanadi. Framework-dependent publish'da
    # bu bayroqlar bilan `dotnet publish` xato beradi, shuning uchun shartli qo'shiladi.
    $publishArgs = @(
        "publish", $uiProject,
        "-c", "Release",
        "-r", $rid,
        "--self-contained", $scLower,
        "-p:PublishSingleFile=true"
    )
    if ($selfContained) {
        $publishArgs += "-p:IncludeNativeLibrariesForSelfExtract=true"
        $publishArgs += "-p:EnableCompressionInSingleFile=true"
    }
    $publishArgs += @("-o", $target)

    & dotnet @publishArgs

    if ($LASTEXITCODE -ne 0) {
        throw "$rid uchun yig'ish muvaffaqiyatsiz tugadi (kod: $LASTEXITCODE)."
    }

    # --- ZIP arxiv ------------------------------------------------------
    $suffix = if ($selfContained) { "selfcontained" } else { "framework" }
    $zipPath = Join-Path $Output "DarsJadvali-legacy-wpf-$rid-$suffix.zip"

    if (Test-Path $zipPath) {
        Remove-Item -Path $zipPath -Force
    }

    Write-Host " ZIP arxiv yasalmoqda: $zipPath"
    Compress-Archive -Path (Join-Path $target "*") -DestinationPath $zipPath -CompressionLevel Optimal

    $sizeMb = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)
    Write-Host " Tayyor: $rid  ($sizeMb MB)" -ForegroundColor Green

    $created += [PSCustomObject]@{ Rid = $rid; Folder = $target; Zip = $zipPath; SizeMb = $sizeMb }
}

Write-Host ""
Write-Host "=========================================================" -ForegroundColor Cyan
Write-Host " YAKUN (ESKI WPF versiyasi)" -ForegroundColor Cyan
Write-Host "=========================================================" -ForegroundColor Cyan
foreach ($item in $created) {
    Write-Host (" {0,-8} -> {1}  ({2} MB)" -f $item.Rid, $item.Zip, $item.SizeMb)
}

Write-Host ""
if ($selfContained) {
    Write-Host " Bu versiya SELF-CONTAINED: foydalanuvchi kompyuteriga hech narsa" -ForegroundColor Green
    Write-Host " o'rnatish shart emas. Arxivni ochib, DarsJadvali.exe ni ishga tushirish kifoya." -ForegroundColor Green
}
else {
    Write-Host " DIQQAT! Bu versiya FRAMEWORK-DEPENDENT." -ForegroundColor Yellow
    Write-Host " Foydalanuvchi kompyuterida .NET 8 DESKTOP RUNTIME o'rnatilgan bo'lishi SHART:" -ForegroundColor Yellow
    Write-Host "   https://dotnet.microsoft.com/download/dotnet/8.0/runtime  (Desktop Runtime)" -ForegroundColor Yellow
    Write-Host "   win-x64 uchun: windowsdesktop-runtime-8.x.x-win-x64.exe" -ForegroundColor Yellow
    Write-Host "   win-x86 uchun: windowsdesktop-runtime-8.x.x-win-x86.exe" -ForegroundColor Yellow
    Write-Host " Aks holda dastur ochilmaydi." -ForegroundColor Yellow
}

Write-Host ""
Write-Host " Eslatma: 32-bitli Windows uchun win-x86, 64-bitli uchun win-x64 versiyasini tarqating."
Write-Host " Ma'lumotlar bazasi foydalanuvchida shu yerda yaratiladi:"
Write-Host "   %LOCALAPPDATA%\DarsJadvali\darsjadvali.db"
Write-Host ""
Write-Host " ESLATMA: bu ESKI WPF dasturi edi." -ForegroundColor Yellow
Write-Host " Yangi (Avalonia) dastur:  .\build\publish-windows.ps1" -ForegroundColor Yellow
Write-Host ""
